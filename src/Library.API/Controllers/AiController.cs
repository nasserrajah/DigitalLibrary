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
            var userId = _me.UserId!.Value;
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);
            var userName = user?.FullName ?? "المستخدم";

            var parsed = AiQueryParser.Parse(request.Question);

            _logger.LogInformation(
                "AI Query from {Email}: '{Question}' | IsFirst={First} | HistoryCount={HCount}",
                user?.Email ?? "unknown",
                request.Question,
                request.IsFirstMessage,
                request.History?.Count ?? 0);

            var candidateBooks = await SearchMultipleStrategiesAsync(parsed, ct);

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

            // بناء نص سياق المحادثة السابقة
            var historyText = "";
            if (!request.IsFirstMessage && request.History != null && request.History.Any())
            {
                historyText = string.Join("\n", request.History.Select(h =>
                    $"{h.Role}: {h.Content}"));
            }

            var answer = await _aiService.GetBookAdviceAsync(
                userName,
                request.Question,
                aiBooks,
                request.IsFirstMessage, // ✅ نمرر حالة الرسالة الأولى
                historyText,            // ✅ نمرر السياق
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
    public bool IsFirstMessage { get; set; } = true;
    public List<ChatHistoryItem>? History { get; set; }
}

public class ChatHistoryItem
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}