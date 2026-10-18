using Library.Application.Common;
using Library.Application.Services;
using Library.Domain.DTOs;
using Library.Domain.Interfaces;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ICurrentUserService _me;
    private readonly AppDbContext _db;
    private readonly ILogger<AiController> _logger;

    private const int MaxBooksForAi = 150;
    private const int MaxDescriptionLength = 150;
    private const int MaxHistoryMessages = 10;

    public AiController(
        IAiService aiService,
        ICurrentUserService me,
        AppDbContext db,
        ILogger<AiController> logger)
    {
        _aiService = aiService;
        _me = me;
        _db = db;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<ApiResponse<object>>> Ask(
        [FromBody] AiAskRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(ApiResponse<object>.Fail("السؤال مطلوب."));

        try
        {
            // ═══════════════════════════════════════════════
            // 1) جلب المستخدم
            // ═══════════════════════════════════════════════
            var userId = _me.UserId!.Value;
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);
            var userName = user?.FullName ?? "المستخدم";

            // ═══════════════════════════════════════════════
            // 2) تحليل السؤال
            // ═══════════════════════════════════════════════
            var parsed = AiQueryParser.Parse(request.Question);

            // 🆕 بناء نص السياق من الرسائل السابقة
            string historyContext = "";
            if (!request.IsFirstMessage &&
                request.History != null &&
                request.History.Any())
            {
                var recent = request.History
                    .TakeLast(MaxHistoryMessages)
                    .Select(h => $"{TranslateRole(h.Role)}: {h.Content}");
                historyContext = string.Join("\n", recent);
            }

            _logger.LogInformation(
                "AI Query from {Email}: '{Question}' | IsFirst={First} | History={HCount} | Keywords=[{Kws}]",
                user?.Email ?? "unknown",
                request.Question,
                request.IsFirstMessage,
                request.History?.Count ?? 0,
                string.Join(", ", parsed.Keywords));

            // ═══════════════════════════════════════════════
            // 3) البحث متعدد الاستراتيجيات
            // ═══════════════════════════════════════════════
            var candidateBooks = await SearchMultipleStrategiesAsync(parsed, ct);

            // ═══════════════════════════════════════════════
            // 4) تحويل الكتب
            // ═══════════════════════════════════════════════
            var aiBooks = candidateBooks
                .Take(MaxBooksForAi)
                .Select(b => new AiBookCandidate
                {
                    Key = b.Id.ToString(),
                    Title = b.Title,
                    Author = b.Author,
                    Description = b.Description != null && b.Description.Length > MaxDescriptionLength
                        ? b.Description.Substring(0, MaxDescriptionLength) + "..."
                        : b.Description,
                    CategoryName = b.Category?.Name,
                    Language = b.Language,
                    Year = b.PublicationDate?.Year,
                    Format = b.Format.ToString()
                })
                .ToList();

            _logger.LogInformation(
                "Sending {Count} books to AI. First={First}",
                aiBooks.Count, request.IsFirstMessage);

            // ═══════════════════════════════════════════════
            // 5) إرسال للـ AI (السؤال الأصلي + isFirstMessage + history)
            // ═══════════════════════════════════════════════
            var answer = await _aiService.GetBookAdviceAsync(
                userName,
                request.Question,
                aiBooks,
                request.IsFirstMessage,
                historyContext,
                ct);

            return Ok(ApiResponse<object>.Ok(new
            {
                answer,
                totalBooksAvailable = aiBooks.Count,
                isFirstMessage = request.IsFirstMessage,
                source = "unified_handler"
            }, "تم"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service error");
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private static string TranslateRole(string role) =>
        role?.ToLower() == "assistant" ? "المساعد" : "المستخدم";

    private async Task<List<Domain.Entities.Book>> SearchMultipleStrategiesAsync(
        ParsedQuery parsed, CancellationToken ct)
    {
        var allBooks = new List<Domain.Entities.Book>();
        var seenIds = new HashSet<Guid>();

        if (parsed.Keywords.Count > 0)
        {
            var keywordBooks = await _db.Books.AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.IsPublished)
                .Where(b =>
                    parsed.Keywords.Any(k =>
                        b.Title.Contains(k) ||
                        b.Author.Contains(k) ||
                        (b.Description != null && b.Description.Contains(k)) ||
                        (b.ISBN != null && b.ISBN.Contains(k))))
                .OrderByDescending(b => b.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            AddUniqueBooks(allBooks, keywordBooks, seenIds);
            _logger.LogInformation("Keyword search: {Count} books", keywordBooks.Count);
        }

        if (parsed.Categories.Count > 0)
        {
            var categoryBooks = await _db.Books.AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.IsPublished)
                .Where(b => b.Category != null &&
                            parsed.Categories.Contains(b.Category.Name))
                .OrderByDescending(b => b.CreatedAt)
                .Take(40)
                .ToListAsync(ct);

            AddUniqueBooks(allBooks, categoryBooks, seenIds);
            _logger.LogInformation("Category search: {Count} books", categoryBooks.Count);
        }

        if (parsed.Languages.Count > 0)
        {
            var languageBooks = await _db.Books.AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.IsPublished)
                .Where(b => b.Language != null &&
                            parsed.Languages.Contains(b.Language))
                .OrderByDescending(b => b.CreatedAt)
                .Take(30)
                .ToListAsync(ct);

            AddUniqueBooks(allBooks, languageBooks, seenIds);
            _logger.LogInformation("Language search: {Count} books", languageBooks.Count);
        }

        if (allBooks.Count < 30)
        {
            var recentBooks = await _db.Books.AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.CreatedAt)
                .Take(30 - allBooks.Count)
                .ToListAsync(ct);

            AddUniqueBooks(allBooks, recentBooks, seenIds);
            _logger.LogInformation("Added recent books: {Count}", recentBooks.Count);
        }

        return allBooks;
    }

    private static void AddUniqueBooks(
        List<Domain.Entities.Book> target,
        List<Domain.Entities.Book> source,
        HashSet<Guid> seenIds)
    {
        foreach (var book in source)
        {
            if (seenIds.Add(book.Id))
                target.Add(book);
        }
    }
}

public class AiAskRequest
{
    public string Question { get; set; } = string.Empty;

    // 🆕 هل هذه أول رسالة في المحادثة؟
    public bool IsFirstMessage { get; set; } = true;

    // 🆕 سجل الرسائل السابقة
    public List<ChatHistoryItem>? History { get; set; }
}

public class ChatHistoryItem
{
    public string Role { get; set; } = "user"; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
}