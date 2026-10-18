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

    // الحد الأقصى للكتب المرسلة للـ AI
    private const int MaxBooksForAi = 150;

    // أقصى طول للوصف
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

            _logger.LogInformation(
                "AI Query from {Email}: '{Question}' | Keywords=[{Kws}] | Categories=[{Cats}] | Languages=[{Langs}]",
                user?.Email ?? "unknown",
                request.Question,
                string.Join(", ", parsed.Keywords),
                string.Join(", ", parsed.Categories),
                string.Join(", ", parsed.Languages));

            // ═══════════════════════════════════════════════
            // 3) البحث متعدد الاستراتيجيات في DB
            // ═══════════════════════════════════════════════
            var candidateBooks = await SearchMultipleStrategiesAsync(parsed, ct);

            _logger.LogInformation(
                "Found {Count} candidate books for the query. Titles: {Titles}",
                candidateBooks.Count,
                string.Join(" | ", candidateBooks.Take(5).Select(b => b.Title)));

            // ═══════════════════════════════════════════════
            // 4) تحويل الكتب إلى AiBookCandidate
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

            // ═══════════════════════════════════════════════
            // 5) إرسال للـ AI مع تعليمات شاملة
            // ═══════════════════════════════════════════════
            var enhancedQuestion = BuildEnhancedQuestion(
                request.Question, parsed, aiBooks.Count);

            var answer = await _aiService.GetBookAdviceAsync(
                userName, enhancedQuestion, aiBooks, ct);

            return Ok(ApiResponse<object>.Ok(new
            {
                answer,
                totalBooksAvailable = aiBooks.Count,
                source = "unified_handler"
            }, "تم"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service error");
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 🔍 البحث متعدد الاستراتيجيات
    // ═══════════════════════════════════════════════════════════
    private async Task<List<Domain.Entities.Book>> SearchMultipleStrategiesAsync(
        ParsedQuery parsed, CancellationToken ct)
    {
        var allBooks = new List<Domain.Entities.Book>();
        var seenIds = new HashSet<Guid>();

        // 1) البحث بالكلمات المفتاحية (عنوان/مؤلف/وصف/ISBN)
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
            _logger.LogInformation("Keyword search: {Count} books found", keywordBooks.Count);
        }

        // 2) البحث بالتصنيفات
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
            _logger.LogInformation("Category search: {Count} books found", categoryBooks.Count);
        }

        // 3) البحث باللغات
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
            _logger.LogInformation("Language search: {Count} books found", languageBooks.Count);
        }

        // 4) إذا كان لدينا أقل من 30 كتاباً → أضف كتباً حديثة كسياق
        if (allBooks.Count < 30)
        {
            var recentBooks = await _db.Books.AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.CreatedAt)
                .Take(30 - allBooks.Count)
                .ToListAsync(ct);

            AddUniqueBooks(allBooks, recentBooks, seenIds);
            _logger.LogInformation("Added recent books for context: {Count}", recentBooks.Count);
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

    // ═══════════════════════════════════════════════════════════
    // 📝 بناء السؤال المحسّن للـ AI
    // ═══════════════════════════════════════════════════════════
    private static string BuildEnhancedQuestion(
        string originalQuestion, ParsedQuery parsed, int booksCount)
    {
        return $"""
            سؤال المستخدم:
            "{originalQuestion}"

            عدد الكتب المتاحة في القائمة: {booksCount}

            ══════════════════════════════════════════════
            🎯 تعليمات الإجابة (اقرأها بعناية):
            ══════════════════════════════════════════════

            1. حدد نوع السؤال:
               • سؤال عن كتاب محدد ("هل عندكم كتاب X؟") → ابحث في Title
               • سؤال عن مؤلف ("من مؤلف X؟") → ابحث في Title واذكر Author
               • سؤال عن محتوى ("عن ماذا يتحدث X؟") → اذكر Description
               • سؤال عن تصنيف ("ما كتب البرمجة؟") → فلتر حسب CategoryName
               • طلب ترشيح ("اقترح لي...") → رشح من القائمة
               • سؤال عام ("مرحبا"، "كيف حالك") → رد بشكل مناسب

            2. قواعد صارمة:
               ⛔ لا تذكر أي كتاب غير موجود في القائمة المرفقة
               ⛔ لا تخترع أسماء مؤلفين أو تواريخ أو تفاصيل
               ⛔ لا تضع أرقاماً عشوائية أو سنوات غير مذكورة

            3. إذا سُئلت عن شيء غير موجود:
               ✅ اعتذر بلطف
               ✅ اذكر 2-3 كتب من القائمة قد تعجبه
               ✅ اقترح البحث بكلمات أخرى

            4. الأجوبة:
               ✅ قصيرة وواضحة
               ✅ ابدأ بتحية باسم المستخدم
               ✅ استخدم تنسيق markdown (bold، lists)
            """;
    }
}

public class AiAskRequest
{
    public string Question { get; set; } = string.Empty;
}