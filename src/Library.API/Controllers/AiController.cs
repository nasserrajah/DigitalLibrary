using Library.Application.Common;
using Library.Application.Services;
using Library.Domain.DTOs;
using Library.Domain.Interfaces;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
            // 1) جلب اسم المستخدم
            var userId = _me.UserId!.Value;
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);
            var userName = user?.FullName ?? "المستخدم";

            // 2) تحليل السؤال
            var parsed = AiQueryParser.Parse(request.Question);

            _logger.LogInformation(
                "AI Query from {Email}: '{Question}' → Intent={Intent}, Categories=[{Cats}], Languages=[{Langs}], Keywords=[{Kws}]",
                user?.Email ?? "unknown",
                request.Question,
                parsed.Intent,
                string.Join(", ", parsed.Categories),
                string.Join(", ", parsed.Languages),
                string.Join(", ", parsed.Keywords));

            // 3) التوجيه حسب النية
            switch (parsed.Intent)
            {
                case AiIntent.Lookup:
                    return await HandleLookupAsync(userName, parsed, ct);

                case AiIntent.Recommend:
                    return await HandleRecommendAsync(userName, parsed, ct);

                default:
                    return await HandleGeneralAsync(userName, request.Question, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service error");
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 🔍 سيناريو 1: المستخدم يبحث عن كتاب محدد
    // ═══════════════════════════════════════════════════════════
    private async Task<ActionResult<ApiResponse<object>>> HandleLookupAsync(
        string userName, ParsedQuery parsed, CancellationToken ct)
    {
        // بحث في DB بكلمات مفتاحية
        List<Domain.Entities.Book> books;
        if (parsed.Keywords.Count > 0)
        {
            books = await _db.Books.AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.IsPublished)
                .Where(b =>
                    parsed.Keywords.Any(k =>
                        b.Title.Contains(k) ||
                        b.Author.Contains(k) ||
                        (b.ISBN != null && b.ISBN.Contains(k))))
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .ToListAsync(ct);
        }
        else
        {
            books = new List<Domain.Entities.Book>();
        }

        // الحالة 1: لم يجد شيئاً
        if (books.Count == 0)
        {
            var allBooks = await _db.Books.AsNoTracking()
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .Select(b => b.Title)
                .ToListAsync(ct);

            var notFoundMsg = $"عذراً {userName}، لم أجد كتاباً يطابق \"{string.Join(' ', parsed.Keywords)}\" في المكتبة.";
            
            if (allBooks.Count > 0)
            {
                notFoundMsg += "\n\nلكن لدينا هذه الكتب، قد تعجبك:\n";
                notFoundMsg += string.Join("\n", allBooks.Select(t => $"• {t}"));
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                answer = notFoundMsg,
                source = "lookup_not_found",
                found = false
            }, "تم"));
        }

        // الحالة 2: وجد كتاباً أو أكثر
        var sb = new StringBuilder();
        if (books.Count == 1)
            sb.AppendLine($"✅ نعم {userName}، هذا الكتاب موجود:");
        else
            sb.AppendLine($"✅ نعم {userName}، وجدت {books.Count} كتب:");

        sb.AppendLine();
        for (int i = 0; i < books.Count; i++)
        {
            var b = books[i];
            if (books.Count > 1) sb.AppendLine($"{i + 1}. 📖 **{b.Title}**");
            else sb.AppendLine($"📖 **{b.Title}**");

            sb.AppendLine($"   ✍️ **المؤلف:** {b.Author}");
            if (b.Category != null) sb.AppendLine($"   📂 **التصنيف:** {b.Category.Name}");
            sb.AppendLine($"   🌐 **اللغة:** {TranslateLanguage(b.Language)}");
            if (b.PublicationDate.HasValue) sb.AppendLine($"   📅 **السنة:** {b.PublicationDate.Value.Year}");
            sb.AppendLine($"   📄 **الصيغة:** {b.Format}");
            if (!string.IsNullOrEmpty(b.Description))
            {
                var desc = b.Description.Length > 300
                    ? b.Description.Substring(0, 300) + "..."
                    : b.Description;
                sb.AppendLine($"   📝 **الوصف:** {desc}");
            }
            if (i < books.Count - 1) sb.AppendLine();
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            answer = sb.ToString().Trim(),
            source = "lookup_direct",
            found = true,
            books = books.Select(b => new { b.Id, b.Title, b.Author })
        }, "تم"));
    }

    // ═══════════════════════════════════════════════════════════
    // 🎯 سيناريو 2: المستخدم يطلب ترشيح (هذا هو محور طلبك!)
    // ═══════════════════════════════════════════════════════════
    private async Task<ActionResult<ApiResponse<object>>> HandleRecommendAsync(
        string userName, ParsedQuery parsed, CancellationToken ct)
    {
        // 1) بناء استعلام التصفية
        var query = _db.Books.AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.IsPublished);

        bool hasFilter = false;

        // تصفية حسب التصنيف
        if (parsed.Categories.Count > 0)
        {
            query = query.Where(b => b.Category != null && parsed.Categories.Contains(b.Category.Name));
            hasFilter = true;
        }

        // تصفية حسب اللغة
        if (parsed.Languages.Count > 0)
        {
            query = query.Where(b => b.Language != null && parsed.Languages.Contains(b.Language));
            hasFilter = true;
        }

        // 2) جلب الكتب المطابقة (حد أقصى 10)
        var filteredBooks = await query
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        // 3) الحالة: لا توجد نتائج
        if (filteredBooks.Count == 0)
        {
            var msg = BuildNoResultsMessage(userName, parsed);
            return Ok(ApiResponse<object>.Ok(new
            {
                answer = msg,
                source = "recommend_no_match",
                filters = new { parsed.Categories, parsed.Languages }
            }, "تم"));
        }

        // 4) الحالة: وجدنا كتباً → نرسل فقط هذه الكتب (5-10) للـ AI
        //    بهذا لا يستطيع الـ AI اختراع كتب أخرى!
        var aiBooks = filteredBooks.Select(b => new AiBookCandidate
        {
            Key = b.Id.ToString(),
            Title = b.Title,
            Author = b.Author,
            Description = b.Description != null && b.Description.Length > 150
                ? b.Description.Substring(0, 150) + "..."
                : b.Description,
            CategoryName = b.Category?.Name,
            Language = b.Language,
            Year = b.PublicationDate?.Year,
            Format = b.Format.ToString()
        }).ToList();

        _logger.LogInformation(
            "Sending {Count} filtered books to AI for recommendation. Books: {Titles}",
            aiBooks.Count,
            string.Join(" | ", aiBooks.Select(b => b.Title)));

        // 5) استدعاء AI مع القائمة المصفّاة فقط
        var aiAnswer = await _aiService.GetBookAdviceAsync(
            userName,
            "قم بترشيح هذه الكتب بأسلوب ودود. اذكر كل كتاب بتفاصيله (العنوان، المؤلف، الوصف). " +
            "هذه هي الكتب الوحيدة التي يمكنك ذكرها:",
            aiBooks,
            ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            answer = aiAnswer,
            source = "recommend_ai_filtered",
            totalBooksAvailable = aiBooks.Count,
            filters = new { parsed.Categories, parsed.Languages }
        }, "تم"));
    }

    // ═══════════════════════════════════════════════════════════
    // 💬 سيناريو 3: سؤال عام (تحية، ماذا تفعل، إلخ)
    // ═══════════════════════════════════════════════════════════
    private async Task<ActionResult<ApiResponse<object>>> HandleGeneralAsync(
        string userName, string question, CancellationToken ct)
    {
        // نرسل 30 كتاباً حديثاً كسياق للـ AI
        var recentBooks = await _db.Books.AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.IsPublished)
            .OrderByDescending(b => b.CreatedAt)
            .Take(30)
            .Select(b => new AiBookCandidate
            {
                Key = b.Id.ToString(),
                Title = b.Title,
                Author = b.Author,
                Description = b.Description != null && b.Description.Length > 100
                    ? b.Description.Substring(0, 100) + "..."
                    : b.Description,
                CategoryName = b.Category != null ? b.Category.Name : null,
                Language = b.Language,
                Year = b.PublicationDate != null ? b.PublicationDate.Value.Year : null,
                Format = b.Format.ToString()
            })
            .ToListAsync(ct);

        var answer = await _aiService.GetBookAdviceAsync(
            userName, question, recentBooks, ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            answer,
            source = "general",
            totalBooksAvailable = recentBooks.Count
        }, "تم"));
    }

    // ═══════════════════════════════════════════════════════════
    // 📝 رسائل "لا توجد نتائج"
    // ═══════════════════════════════════════════════════════════
    private async Task<string> BuildNoResultsMessage(string userName, ParsedQuery parsed)
    {
        var sb = new StringBuilder();
        sb.Append($"عذراً {userName}، ");

        if (parsed.Categories.Any() && parsed.Languages.Any())
        {
            var catNames = string.Join(" أو ", parsed.Categories);
            var langNames = string.Join(" أو ", parsed.Languages.Select(TranslateLanguage));
            sb.Append($"لا توجد كتب من تصنيف \"{catNames}\" باللغة {langNames} حالياً.");
        }
        else if (parsed.Categories.Any())
        {
            sb.Append($"لا توجد كتب في تصنيف \"{string.Join(" أو ", parsed.Categories)}\" حالياً.");
        }
        else if (parsed.Languages.Any())
        {
            sb.Append($"لا توجد كتب باللغة {string.Join(" أو ", parsed.Languages.Select(TranslateLanguage))} حالياً.");
        }
        else
        {
            sb.Append("لم أجد كتباً تطابق طلبك.");
        }

        sb.AppendLine();
        sb.AppendLine();

        // نقترح ما هو موجود فعلاً
        var availableCategories = await _db.Categories.AsNoTracking()
            .Where(c => c.Books.Any(b => b.IsPublished))
            .Select(c => new { c.Name, Count = c.Books.Count(b => b.IsPublished) })
            .OrderByDescending(c => c.Count)
            .Take(6)
            .ToListAsync();

        if (availableCategories.Count > 0)
        {
            sb.AppendLine("لكن لدينا هذه التصنيفات المتاحة:");
            foreach (var cat in availableCategories)
                sb.AppendLine($"• {TranslateCategory(cat.Name)} ({cat.Count} كتاب)");
        }

        return sb.ToString().Trim();
    }

    // ═══════════════════════════════════════════════════════════
    // 🌐 تحويل رموز اللغات إلى عربي
    // ═══════════════════════════════════════════════════════════
    private static string TranslateLanguage(string? lang) => lang?.ToLower() switch
    {
        "ar" => "العربية",
        "en" => "الإنجليزية",
        "fr" => "الفرنسية",
        "de" => "الألمانية",
        "es" => "الإسبانية",
        "zh" => "الصينية",
        "ja" => "اليابانية",
        null or "" => "غير محددة",
        _ => lang
    };

    // ═══════════════════════════════════════════════════════════
    // 📂 تحويل أسماء التصنيفات للإنجليزية إلى عربي
    // ═══════════════════════════════════════════════════════════
    private static string TranslateCategory(string category) => category switch
    {
        "Programming" => "البرمجة",
        "Technology" => "التقنية",
        "Science" => "العلوم",
        "Mathematics" => "الرياضيات",
        "History" => "التاريخ",
        "Literature" => "الأدب",
        "Education" => "التعليم",
        "Business" => "الأعمال",
        "Self Development" => "تطوير الذات",
        "Psychology" => "علم النفس",
        "Languages" => "اللغات",
        "Religion" => "الدين",
        "Health" => "الصحة",
        "Sports" => "الرياضة",
        "Fiction" => "الخيال",
        "Novels" => "الروايات",
        "Children" => "الأطفال",
        _ => category
    };
}

public class AiAskRequest
{
    public string Question { get; set; } = string.Empty;
}