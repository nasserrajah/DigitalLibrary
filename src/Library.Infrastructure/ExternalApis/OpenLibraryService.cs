using System.Text.Json;
using Library.Infrastructure.ExternalApis.DTOs;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.ExternalApis;

public class OpenLibraryService : IBookMetadataService
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenLibraryService> _log;
    private const string BaseUrl = "https://openlibrary.org";
    private const string CoversUrl = "https://covers.openlibrary.org";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public OpenLibraryService(HttpClient http, ILogger<OpenLibraryService> log)
    {
        _http = http;
        _log = log;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SmartLibrary/1.0 (contact@smartlib.local)");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ═══════════════════════════════════════════════════════
    // 1. البحث العام (عنوان / مؤلف)
    // ═══════════════════════════════════════════════════════
    public async Task<List<BookMetadataDto>> SearchBooksAsync(
        string query, int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();

        try
        {
            var url = $"{BaseUrl}/search.json" +
                      $"?q={Uri.EscapeDataString(query)}" +
                      $"&limit={limit}" +
                      $"&fields=key,title,author_name,first_publish_year,cover_i,isbn,publisher,language,subject,number_of_pages_median";

            _log.LogInformation("OpenLibrary search: {Url}", url);

            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("OpenLibrary returned {Status}", response.StatusCode);
                return new();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(json, JsonOpts);

            if (parsed?.Docs == null) return new();

            var results = parsed.Docs
                .Where(d => !string.IsNullOrWhiteSpace(d.Title))
                .Select(MapSearchDocToDto)
                .ToList();

            // إثراء البيانات: جلب الأوصاف للكتب الأولى
            var enriched = await EnrichWithDescriptionsAsync(results, maxBooks: 5, ct);
            return enriched;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OpenLibrary search failed: {Query}", query);
            return new();
        }
    }

    // ═══════════════════════════════════════════════════════
    // 2. البحث بالـ ISBN
    // ═══════════════════════════════════════════════════════
    public async Task<BookMetadataDto?> GetByIsbnAsync(
        string isbn, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return null;

        try
        {
            var url = $"{BaseUrl}/search.json" +
                      $"?isbn={Uri.EscapeDataString(isbn)}" +
                      $"&limit=1" +
                      $"&fields=key,title,author_name,first_publish_year,cover_i,isbn,publisher,language,subject,number_of_pages_median";

            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(json, JsonOpts);

            var doc = parsed?.Docs?.FirstOrDefault();
            if (doc == null) return null;

            var dto = MapSearchDocToDto(doc);
            dto.Description = await TryFetchDescriptionAsync(doc.Key, ct);
            return dto;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OpenLibrary ISBN lookup failed: {Isbn}", isbn);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════
    // 3. جلب كتب تصنيف معيّن — يستخدم SEARCH endpoint (أغنى بيانات)
    // ═══════════════════════════════════════════════════════
    public async Task<List<BookMetadataDto>> GetBooksBySubjectAsync(
        string subject, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subject)) return new();

        try
        {
            // ✅ نستخدم search endpoint مع subject:xxx — يعطي بيانات أغنى بكثير
            // Publisher, ISBN, Language, PublicationYear — كلها موجودة!
            var url = $"{BaseUrl}/search.json" +
                      $"?q=subject:{Uri.EscapeDataString(subject)}" +
                      $"&limit={limit}" +
                      $"&fields=key,title,author_name,first_publish_year,cover_i,isbn,publisher,language,subject,number_of_pages_median";

            _log.LogInformation("OpenLibrary subject search: {Url}", url);

            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("OpenLibrary subject search {Subject} returned {Status}",
                    subject, response.StatusCode);
                return new();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(json, JsonOpts);

            if (parsed?.Docs == null) return new();

            var results = parsed.Docs
                .Where(d => !string.IsNullOrWhiteSpace(d.Title))
                .Select(MapSearchDocToDto)
                .ToList();

            _log.LogInformation("Found {Count} books for subject {Subject}",
                results.Count, subject);

            // إثراء الأوصاف (أول 5 كتب فقط لتجنب البطء)
            var enriched = await EnrichWithDescriptionsAsync(results, maxBooks: 5, ct);
            return enriched;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OpenLibrary subject fetch failed: {Subject}", subject);
            return new();
        }
    }

    // ═══════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// إثراء الكتب بالأوصاف من work endpoint (متوازي)
    /// </summary>
    private async Task<List<BookMetadataDto>> EnrichWithDescriptionsAsync(
        List<BookMetadataDto> books, int maxBooks, CancellationToken ct)
    {
        if (!books.Any()) return books;

        var targetBooks = books.Take(maxBooks).ToList();
        var tasks = new List<Task>();

        foreach (var book in targetBooks)
        {
            // نحتاج workKey — نستخرجه من الموقع إذا وُجد
            // لكن حالياً ليس لدينا workKey مباشرة في الـ DTO
            // لذلك نستخدم ISBN لجلب الوصف
            if (!string.IsNullOrEmpty(book.ISBN))
            {
                tasks.Add(Task.Run(async () =>
                {
                    var desc = await TryFetchDescriptionByIsbnAsync(book.ISBN!, ct);
                    if (!string.IsNullOrEmpty(desc))
                        book.Description = desc;
                }, ct));
            }
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Some descriptions failed to load");
        }

        return books;
    }

    /// <summary>
    /// جلب الوصف بـ ISBN
    /// </summary>
    private async Task<string?> TryFetchDescriptionByIsbnAsync(
        string isbn, CancellationToken ct)
    {
        try
        {
            // البحث عن الـ work من الـ ISBN
            var url = $"{BaseUrl}/isbn/{isbn}.json";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ابحث عن works
            if (root.TryGetProperty("works", out var works) &&
                works.GetArrayLength() > 0)
            {
                var workKey = works[0].GetProperty("key").GetString();
                return await FetchWorkDescriptionAsync(workKey, ct);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// جلب وصف work من workKey
    /// </summary>
    private async Task<string?> TryFetchDescriptionAsync(
        string? workKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workKey)) return null;
        return await FetchWorkDescriptionAsync(workKey, ct);
    }

    private async Task<string?> FetchWorkDescriptionAsync(
        string? workKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workKey)) return null;

        try
        {
            var url = $"{BaseUrl}{workKey}.json";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("description", out var desc))
            {
                string? text = null;

                if (desc.ValueKind == JsonValueKind.String)
                    text = desc.GetString();
                else if (desc.ValueKind == JsonValueKind.Object &&
                         desc.TryGetProperty("value", out var v))
                    text = v.GetString();

                // اقتطاع الوصف الطويل
                if (!string.IsNullOrEmpty(text) && text.Length > 2000)
                    text = text.Substring(0, 2000) + "...";

                return text;
            }
        }
        catch { }

        return null;
    }

    private static BookMetadataDto MapSearchDocToDto(OpenLibrarySearchDoc doc) => new()
    {
        Title = doc.Title,
        Author = doc.AuthorName.FirstOrDefault() ?? "Unknown",
        Publisher = doc.Publisher.FirstOrDefault(),
        PublishYear = doc.FirstPublishYear,
        ISBN = doc.Isbn?.FirstOrDefault(),
        Language = MapLanguage(doc.Language?.FirstOrDefault()),
        CoverUrl = doc.CoverId.HasValue
            ? $"{CoversUrl}/b/id/{doc.CoverId}-M.jpg"
            : null,
        Subjects = doc.Subject?.Take(10).ToList() ?? new(),
        NumberOfPages = doc.NumberOfPagesMedian,
        Source = "OpenLibrary"
    };

    private static string MapLanguage(string? code) => code?.ToLowerInvariant() switch
    {
        "eng" or "en" => "en",
        "ara" or "ar" => "ar",
        "fre" or "fra" or "fr" => "fr",
        "spa" or "es" => "es",
        "ger" or "deu" or "de" => "de",
        "ita" or "it" => "it",
        "rus" or "ru" => "ru",
        "chi" or "zho" or "zh" => "zh",
        "jpn" or "ja" => "ja",
        _ => "en"
    };
}