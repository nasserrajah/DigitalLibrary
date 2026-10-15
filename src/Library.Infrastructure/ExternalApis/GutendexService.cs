using System.Text.Json;
using System.Xml.Linq;
using Library.Infrastructure.ExternalApis.DTOs;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.ExternalApis;

public class GutendexService : IGutendexService
{
    private readonly HttpClient _http;
    private readonly ILogger<GutendexService> _log;

    // ✅ نستخدم Gutenberg مباشرة (لا Cloudflare)
    private const string GutenbergBase = "https://www.gutenberg.org";

    public GutendexService(HttpClient http, ILogger<GutendexService> log)
    {
        _http = http;
        _log = log;
        // ✅ User-Agent مثل متصفح حقيقي
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
    // 2. حسب الموضوع (topic) — نستخدم query مباشرة
    // ═══════════════════════════════════════════════════════
    public async Task<List<BookMetadataDto>> GetByTopicAsync(
        string topic, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topic)) return new();

        // نحوّل الموضوع إلى query عادي
        var url = $"{GutenbergBase}/ebooks/search.opds/?query={Uri.EscapeDataString(topic)}&sort_order=downloads";
        return await FetchOpdsAsync(url, limit, ct);
    }

    // ═══════════════════════════════════════════════════════
    // 3. حسب ID
    // ═══════════════════════════════════════════════════════
    public async Task<BookMetadataDto?> GetByIdAsync(
        int gutenbergId, CancellationToken ct = default)
    {
        try
        {
            var url = $"{GutenbergBase}/ebooks/{gutenbergId}.epub3.images";
            return await Task.FromResult<BookMetadataDto?>(new BookMetadataDto
            {
                GutenbergId = gutenbergId,
                DownloadUrl = url,
                Source = "Gutenberg"
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "GetById failed: {Id}", gutenbergId);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════
    // Helpers — Parse OPDS Atom feed
    // ═══════════════════════════════════════════════════════
    private async Task<List<BookMetadataDto>> FetchOpdsAsync(
        string url, int limit, CancellationToken ct)
    {
        try
        {
            _log.LogInformation("Gutenberg OPDS: {Url}", url);

            var response = await _http.GetAsync(url, ct);
            _log.LogInformation("Status: {Status}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Gutenberg returned {Status}", response.StatusCode);
                return new();
            }

            var xml = await response.Content.ReadAsStringAsync(ct);
            _log.LogInformation("Response length: {Len}", xml.Length);

            return ParseOpds(xml, limit);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gutenberg fetch failed");
            return new();
        }
    }

    private List<BookMetadataDto> ParseOpds(string xml, int limit)
    {
        var books = new List<BookMetadataDto>();

        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace dc = "http://purl.org/dc/terms/";
            XNamespace opds = "http://opds-spec.org/2010/catalog";

            var entries = doc.Descendants(atom + "entry").Take(limit);

            foreach (var entry in entries)
            {
                var title = entry.Element(atom + "title")?.Value ?? "";
                var author = entry.Element(atom + "author")?.Element(atom + "name")?.Value ?? "Unknown";
                var summary = entry.Element(atom + "summary")?.Value
                              ?? entry.Element(atom + "content")?.Value;

                // ID من الرابط
                var idLink = entry.Elements(atom + "link")
                    .FirstOrDefault(l => (string?)l.Attribute("rel") == "alternate")
                    ?.Attribute("href")?.Value;

                int? gutenbergId = null;
                if (!string.IsNullOrEmpty(idLink))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(idLink, @"/ebooks/(\d+)");
                    if (match.Success) gutenbergId = int.Parse(match.Groups[1].Value);
                }

                // EPUB link
                string? epubUrl = null;
                string? coverUrl = null;

                foreach (var link in entry.Elements(atom + "link"))
                {
                    var type = (string?)link.Attribute("type");
                    var href = (string?)link.Attribute("href");
                    var rel = (string?)link.Attribute("rel");

                    if (type == "application/epub+zip" && href != null)
                        epubUrl = href;

                    if ((type?.StartsWith("image/") ?? false) && href != null)
                        coverUrl = href;

                    if (rel == "http://opds-spec.org/image" && href != null)
                        coverUrl = href;
                }

                // إذا لا يوجد EPUB، جرب TXT
                if (epubUrl == null)
                {
                    var txtLink = entry.Elements(atom + "link")
                        .FirstOrDefault(l => (string?)l.Attribute("type") == "text/plain; charset=utf-8");
                    epubUrl = (string?)txtLink?.Attribute("href");
                }

                // إذا لا يوجد رابط تحميل، جرب رابط مباشر من ID
                if (epubUrl == null && gutenbergId.HasValue)
                {
                    epubUrl = $"{GutenbergBase}/ebooks/{gutenbergId}.epub3.images";
                }

                // اللغة
                var language = entry.Element(dc + "language")?.Value ?? "en";

                books.Add(new BookMetadataDto
                {
                    Title = title,
                    Author = author,
                    Description = summary,
                    Language = language,
                    CoverUrl = coverUrl,
                    GutenbergId = gutenbergId,
                    DownloadUrl = epubUrl,
                    Source = "Gutenberg"
                });
            }

            _log.LogInformation("Parsed {Count} books from OPDS", books.Count);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to parse OPDS");
        }

        return books;
    }
}