using System.Xml.Linq;
using Library.Infrastructure.ExternalApis.DTOs;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.ExternalApis;

public class GutendexService : IGutendexService
{
    private readonly HttpClient _http;
    private readonly ILogger<GutendexService> _log;

    private const string GutenbergBase = "https://www.gutenberg.org";

    public GutendexService(HttpClient http, ILogger<GutendexService> log)
    {
        _http = http;
        _log = log;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept",
            "application/atom+xml, application/xml, text/xml, */*");
        _http.Timeout = TimeSpan.FromSeconds(45);
    }

    // ═══════════════════════════════════════════════════════
    // 1. بحث
    // ═══════════════════════════════════════════════════════
    public async Task<List<BookMetadataDto>> SearchAsync(
        string query, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();

        var url = $"{GutenbergBase}/ebooks/search.opds/?query={Uri.EscapeDataString(query)}&sort_order=downloads";
        return await FetchOpdsAsync(url, limit, ct);
    }

    // ═══════════════════════════════════════════════════════
    // 2. حسب الموضوع
    // ═══════════════════════════════════════════════════════
    public async Task<List<BookMetadataDto>> GetByTopicAsync(
        string topic, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topic)) return new();

        var url = $"{GutenbergBase}/ebooks/search.opds/?query={Uri.EscapeDataString(topic)}&sort_order=downloads";
        return await FetchOpdsAsync(url, limit, ct);
    }

    // ═══════════════════════════════════════════════════════
    // 3. حسب ID
    // ═══════════════════════════════════════════════════════
    public Task<BookMetadataDto?> GetByIdAsync(
        int gutenbergId, CancellationToken ct = default)
    {
        return Task.FromResult<BookMetadataDto?>(new BookMetadataDto
        {
            GutenbergId = gutenbergId,
            DownloadUrl = $"{GutenbergBase}/ebooks/{gutenbergId}.epub3.images",
            CoverUrl = $"https://www.gutenberg.org/cache/epub/{gutenbergId}/pg{gutenbergId}.cover.medium.jpg",
            Source = "Gutenberg"
        });
    }

    // ═══════════════════════════════════════════════════════
    // Fetch OPDS Feed
    // ═══════════════════════════════════════════════════════
    private async Task<List<BookMetadataDto>> FetchOpdsAsync(
        string url, int limit, CancellationToken ct)
    {
        try
        {
            _log.LogInformation("Gutenberg OPDS: {Url}", url);

            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Gutenberg returned {Status}", response.StatusCode);
                return new();
            }

            var xml = await response.Content.ReadAsStringAsync(ct);
            var books = ParseOpds(xml, limit);

            // إثراء أول 10 كتب بـ RDF
            var enriched = await EnrichWithDetailsAsync(books.Take(10).ToList(), ct);

            if (books.Count > 10)
                enriched.AddRange(books.Skip(10));

            return enriched;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gutenberg fetch failed");
            return new();
        }
    }

    // ═══════════════════════════════════════════════════════
    // ✅ Enrich من cache RDF المباشر
    // ═══════════════════════════════════════════════════════
    private async Task<List<BookMetadataDto>> EnrichWithDetailsAsync(
        List<BookMetadataDto> books, CancellationToken ct)
    {
        if (books.Count == 0) return books;

        var tasks = books.Select(async book =>
        {
            if (book.GutenbergId == null) return;

            try
            {
                // ✅ مسار مباشر بدون redirect
                var id = book.GutenbergId.Value;
                var url = $"{GutenbergBase}/cache/epub/{id}/pg{id}.rdf";
                
                _log.LogInformation("Fetching RDF: {Url}", url);
                
                var response = await _http.GetAsync(url, ct);
                
                _log.LogInformation("RDF status: {Status} for ID {Id}", 
                    response.StatusCode, id);
                
                if (!response.IsSuccessStatusCode) 
                {
                    // جرب المسار البديل
                    url = $"{GutenbergBase}/ebooks/{id}.rdf";
                    _log.LogInformation("Trying alternate: {Url}", url);
                    response = await _http.GetAsync(url, ct);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _log.LogWarning("Both RDF paths failed for ID {Id}", id);
                        book.Author = "Unknown";
                        return;
                    }
                }

                var xml = await response.Content.ReadAsStringAsync(ct);
                _log.LogInformation("RDF length: {Len} for ID {Id}", xml.Length, id);

                var doc = XDocument.Parse(xml);

                // ═══ المؤلف ═══
                // المسار: dcterms:creator → pgterms:agent → pgterms:name
                var creatorEl = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "creator");

                if (creatorEl != null)
                {
                    var nameEl = creatorEl.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == "name");

                    if (nameEl != null && !string.IsNullOrWhiteSpace(nameEl.Value))
                    {
                        var name = nameEl.Value.Trim();
                        _log.LogInformation("Found author: {Name} for ID {Id}", name, id);
                        book.Author = name;
                    }
                    else
                    {
                        _log.LogWarning("No name in creator for ID {Id}", id);
                        book.Author = "Unknown";
                    }
                }
                else
                {
                    _log.LogWarning("No creator element for ID {Id}", id);
                    book.Author = "Unknown";
                }

                // ═══ الناشر ═══
                var pubEl = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "publisher");
                if (pubEl != null && !string.IsNullOrWhiteSpace(pubEl.Value))
                    book.Publisher = pubEl.Value.Trim().TrimEnd('.');

                // ═══ اللغة ═══
                var langEl = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "language");
                if (langEl != null)
                {
                    var langVal = langEl.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == "value")?.Value;
                    if (!string.IsNullOrWhiteSpace(langVal))
                        book.Language = langVal.Trim();
                }

                // ═══ Subjects ═══
                var subjects = doc.Descendants()
                    .Where(e => e.Name.LocalName == "subject")
                    .SelectMany(s => s.Descendants()
                        .Where(e => e.Name.LocalName == "value"))
                    .Select(e => e.Value.Trim())
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Take(5)
                    .ToList();

                if (subjects.Any())
                    book.Subjects = subjects;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "RDF enrichment failed for ID {Id}", book.GutenbergId);
                book.Author = "Unknown";
            }
        });

        await Task.WhenAll(tasks);
        return books;
    }

    // ═══════════════════════════════════════════════════════
    // Parse OPDS
    // ═══════════════════════════════════════════════════════
    private List<BookMetadataDto> ParseOpds(string xml, int limit)
    {
        var books = new List<BookMetadataDto>();

        try
        {
            var doc = XDocument.Parse(xml);

            var entries = doc.Descendants()
                .Where(e => e.Name.LocalName == "entry")
                .ToList();

            _log.LogInformation("Found {Count} raw entries", entries.Count);

            foreach (var entry in entries)
            {
                var title = entry.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "title")
                    ?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(title)) continue;

                int? gutenbergId = null;
                string? epubUrl = null;

                foreach (var link in entry.Elements().Where(e => e.Name.LocalName == "link"))
                {
                    var href = (string?)link.Attribute("href");
                    var type = (string?)link.Attribute("type");

                    if (string.IsNullOrEmpty(href)) continue;

                    if (!gutenbergId.HasValue)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(href, @"/ebooks/(\d+)");
                        if (match.Success) gutenbergId = int.Parse(match.Groups[1].Value);
                    }

                    if (type != null && type.Contains("epub"))
                        epubUrl = href;
                }

                if (gutenbergId == null) continue;

                if (epubUrl == null)
                    epubUrl = $"{GutenbergBase}/ebooks/{gutenbergId}.epub3.images";

                var coverUrl = $"https://www.gutenberg.org/cache/epub/{gutenbergId}/pg{gutenbergId}.cover.medium.jpg";

                books.Add(new BookMetadataDto
                {
                    Title = title,
                    Author = "Unknown",  // سيُحدَّث من RDF
                    CoverUrl = coverUrl,
                    GutenbergId = gutenbergId,
                    DownloadUrl = epubUrl,
                    Source = "Gutenberg"
                });

                if (books.Count >= limit) break;
            }

            _log.LogInformation("Parsed {Count} valid books", books.Count);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to parse OPDS");
        }

        return books;
    }
}