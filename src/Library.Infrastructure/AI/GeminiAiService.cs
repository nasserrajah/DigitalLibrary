using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Library.Domain.DTOs;
using Library.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.AI;

public sealed class GeminiAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiAiService> _logger;

    private const int MaxBooksForAi = 500;

    public GeminiAiService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<GeminiAiService> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is missing.");
        _model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        _http.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/v1beta/");
        if (_http.Timeout == TimeSpan.FromSeconds(100))
            _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<string> GetBookAdviceAsync(
        string userName,
        string question,
        IEnumerable<AiBookCandidate> availableBooks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question is required.", nameof(question));

        var candidates = availableBooks.Take(MaxBooksForAi).ToList();
        var validKeys = candidates.Select(c => c.Key).ToHashSet();

        var booksJson = JsonSerializer.Serialize(candidates, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // ⚠️ تم استخدام $$""" (دولارين) للسماح بأقواس JSON الحرفية { }
        var prompt = $$"""
            أنت "سند" — المساعد الذكي في مكتبة Smart Library.
            أنت ودود، ذكي، وتتحدث بأسلوب طبيعي كأنك صديق قارئ.
            اسم المستخدم: "{{userName}}".

            ═══════════════════════════════════════════════════════
            📋 قاعدة البيانات (الكتب المتاحة فعلاً):
            ═══════════════════════════════════════════════════════
            {{booksJson}}

            ═══════════════════════════════════════════════════════
            💬 سؤال المستخدم:
            ═══════════════════════════════════════════════════════
            "{{question}}"

            ═══════════════════════════════════════════════════════
            🎯 قواعد شخصيتك:
            ═══════════════════════════════════════════════════════
            1. أنت ودود ومتحمس — تستخدم إيموجي باعتدال ✅📚🎯
            2. تتكلم بشكل طبيعي كأنك في محادثة حقيقية
            3. تبدأ كل رد بتحية باسم المستخدم إذا كانت أول رسالة
            4. ترد باللغة العربية الفصحى المبسطة
            5. تكون مختصراً (2-5 جمل عادةً) إلا إذا طلب تفاصيل

            ═══════════════════════════════════════════════════════
            🎯 قواعد الإجابة (CRITICAL):
            ═══════════════════════════════════════════════════════

            ✅ السيناريو 1: يسأل عن كتاب **موجود** في القائمة
               → قل نعم بثقة
               → اذكر: العنوان، المؤلف، التصنيف، اللغة، السنة
               → أعطِ وصفاً موجزاً
               → أمثلة:
                 • "هل عندكم كتاب الرهائن الثلاث؟" → "نعم {{userName}}! 📖 كتاب الرهائن الثلاث متوفر، للمؤلف جون بوكان، في قسم الأدب..."
                 • "أريد كتاب Clean Code" → "عندنا! Clean Code لـ Robert Martin..."

            ✅ السيناريو 2: يسأل عن كتاب **غير موجود**
               → اعتذر بلطف
               → قل "قريباً سنضيفه"
               → اقترح 2-3 كتب مشابهة من القائمة
               → أمثلة:
                 • "هل عندكم Harry Potter؟" → "عذراً {{userName}}، Harry Potter غير موجود حالياً، لكننا نعمل على إضافته قريباً. في هذه الأثناء، لدينا [رواية كذا] التي قد تعجبك 📚"
                 • "هل عندكم كتاب فيزياء الكم؟" → "لم نضفه بعد، لكننا سنضيفه قريباً! حالياً لديك [كتب العلوم]..."

            ✅ السيناريو 3: يسأل عن مؤلف
               → ابحث عن كتب المؤلف في القائمة
               → إذا وجدت: اذكرها
               → إذا لم تجد: اعتذر
               → أمثلة:
                 • "من مؤلف الرهائن الثلاث؟" → "الكاتب هو جون بوكان 🖊️"
                 • "ما كتب أحمد خالد توفيق؟" → "عذراً، لا توجد كتب لأحمد خالد توفيق حالياً، لكننا سنضيفها قريباً"

            ✅ السيناريو 4: يسأل عن تصنيف
               → اذكر عدد الكتب في التصنيف
               → اذكر 3-5 عناوين منها
               → أمثلة:
                 • "ما كتب البرمجة؟" → "لديك 15 كتاباً في البرمجة! منها: Clean Code، Python Crash Course، ... 🎯"

            ✅ السيناريو 5: يطلب ترشيح
               → اسأله عن اهتماماته أو رشح من القائمة مباشرة
               → أمثلة:
                 • "اقترح لي كتاباً" → "بكل سرور! ما الذي تحب قراءته؟ روايات؟ تطوير ذات؟ تاريخ؟"
                 • "اقترح رواية" → "أنصحك بـ [رواية] للمؤلف [X]، فهي..."

            ✅ السيناريو 6: تحية أو سؤال عام
               → رد بشكل ودود
               → عرّف بنفسك
               → أمثلة:
                 • "مرحبا" → "أهلاً {{userName}}! 👋 أنا سند، مساعدك في المكتبة. كيف أساعدك اليوم؟"
                 • "كيف حالك" → "بخير الحمد لله! جاهز لمساعدتك في اختيار كتابك القادم 📚"

            ✅ السيناريو 7: أسئلة عن المكتبة نفسها
               → اشرح بإيجاز
               → أمثلة:
                 • "كم كتاب في المكتبة؟" → "لديك {{candidates.Count}} كتاباً في المكتبة حالياً"
                 • "كيف أستعير كتاباً؟" → "ببساطة اضغط على الكتاب ثم زر القراءة 📖"

            ═══════════════════════════════════════════════════════
            ⛔ محاذير صارمة:
            ═══════════════════════════════════════════════════════
            ❌ لا تذكر أي كتاب غير موجود في القائمة أعلاه
            ❌ لا تخترع مؤلفين، سنوات، أو تفاصيل
            ❌ لا تعطي إجابات طويلة مملة (3-5 أسطر كافية)
            ❌ لا تكرر نفس الرد لكل سؤال
            ❌ لا تستخدم كلمات رسمية مثل "بصفتي مساعداً ذكياً"

            ═══════════════════════════════════════════════════════
            📋 صيغة الإجابة (JSON):
            ═══════════════════════════════════════════════════════

            {
              "reply": "نص الإجابة بالعربية — طبيعي وودود",
              "mentionedBookKeys": ["key1", "key2"],
              "intent": "book_found" | "book_not_found" | "recommendation" | "greeting" | "info" | "category" | "author"
            }

            ═══════════════════════════════════════════════════════
            📚 أمثلة على الحوارات (تعلم منها):
            ═══════════════════════════════════════════════════════

            مثال 1:
            المستخدم: "مرحبا"
            الرد: {"reply": "أهلاً {{userName}}! 👋 أنا سند، مساعدك الذكي. كيف أقدر أساعدك اليوم؟ عندي كتب رائعة في تصنيفات متنوعة 📚", "mentionedBookKeys": [], "intent": "greeting"}

            مثال 2:
            المستخدم: "هل عندكم كتاب الرهائن الثلاث؟"
            الرد: {"reply": "نعم {{userName}}! 🎯 كتاب «الرهائن الثلاث» متوفر في مكتبتنا. المؤلف: جون بوكان، التصنيف: الأدب. كتاب مميز جداً، أنصحك بقراءته!", "mentionedBookKeys": ["<real-key-here>"], "intent": "book_found"}

            مثال 3:
            المستخدم: "أريد كتاب Harry Potter"
            الرد: {"reply": "عذراً {{userName}}، Harry Potter غير متوفر حالياً. لكننا نعمل على إضافته قريباً 📚 في هذه الأثناء، لدينا [كتاب آخر من نفس التصنيف]، قد يعجبك!", "mentionedBookKeys": [], "intent": "book_not_found"}

            مثال 4:
            المستخدم: "اقترح لي رواية"
            الرد: {"reply": "بكل سرور {{userName}}! 🎯 ما الذي تحب؟ روايات تاريخية؟ رومانسية؟ غامضة؟ أخبرني اهتماماتك وسأختار لك الأنسب 📖", "mentionedBookKeys": [], "intent": "recommendation"}

            مثال 5:
            المستخدم: "كم كتاب في قسم البرمجة؟"
            الرد: {"reply": "لدينا [count] كتب في قسم البرمجة! 💻 منها: [اسم 1]، [اسم 2]، [اسم 3]. هل تريد تفاصيل أحدها؟", "mentionedBookKeys": [], "intent": "category"}

            الآن أجب على سؤال المستخدم أعلاه بنفس الأسلوب.
            """;

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                temperature = 0.1,  // منخفض جداً = أقل عشوائية
                maxOutputTokens = 900,
                responseMimeType = "application/json"
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"models/{_model}:generateContent")
        { Content = JsonContent.Create(body) };

        request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to Gemini API");
            throw new InvalidOperationException(
                "فشل الاتصال بخدمة الذكاء الاصطناعي.", ex);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API {Status}: {Body}",
                response.StatusCode, json);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new InvalidOperationException("المساعد مشغول. حاول بعد قليل.");
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("مفتاح الذكاء الاصطناعي غير صالح.");

            throw new InvalidOperationException(
                $"خدمة الذكاء الاصطناعي أعادت خطأ ({(int)response.StatusCode}).");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("candidates", out var cands) ||
                cands.GetArrayLength() == 0)
                throw new InvalidOperationException("لم يُرجع المساعد أي إجابة.");

            var parts = cands[0].GetProperty("content").GetProperty("parts");
            var rawText = string.Join("\n",
                parts.EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _))
                    .Select(p => p.GetProperty("text").GetString())
                    .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (string.IsNullOrWhiteSpace(rawText))
                throw new InvalidOperationException("المساعد أعاد إجابة فارغة.");

            var cleaned = ExtractJson(rawText);
            _logger.LogInformation("AI raw response: {Raw}", cleaned);

            using var aiDoc = JsonDocument.Parse(cleaned);

            var intent = aiDoc.RootElement.TryGetProperty("intent", out var i)
                ? i.GetString() ?? "not_found"
                : "not_found";

            var reply = aiDoc.RootElement.TryGetProperty("reply", out var r)
                ? r.GetString() ?? ""
                : "";

            var mentionedKeys = new List<string>();
            if (aiDoc.RootElement.TryGetProperty("mentionedBookKeys", out var mk) &&
                mk.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in mk.EnumerateArray())
                {
                    var k = item.GetString();
                    if (!string.IsNullOrWhiteSpace(k)) mentionedKeys.Add(k);
                }
            }

            // التحقق الصارم
            var invalidKeys = mentionedKeys.Where(k => !validKeys.Contains(k)).ToList();

            if (invalidKeys.Any())
            {
                _logger.LogWarning(
                    "⚠️ AI hallucinated {Count} keys: {Keys}. Rejecting.",
                    invalidKeys.Count, string.Join(", ", invalidKeys));

                return $"عذراً {userName}، لم أجد كتباً مطابقة لطلبك في المكتبة حالياً.";
            }

            // إذا AI قال "not_found" لكن ذكر كتباً → تناقض → رفض
            if (intent == "not_found" && mentionedKeys.Any())
            {
                _logger.LogWarning("AI contradiction: intent=not_found but mentioned keys");
                return $"عذراً {userName}، لم أجد كتباً مطابقة لطلبك في المكتبة حالياً.";
            }

            if (string.IsNullOrWhiteSpace(reply))
                return $"عذراً {userName}، لم أستطع تكوين إجابة. حاول مرة أخرى.";

            return reply;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to parse Gemini response");
            throw new InvalidOperationException("فشل قراءة استجابة المساعد.", ex);
        }
    }

    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                return trimmed.Substring(start, end - start + 1);
        }
        if (trimmed.StartsWith("{")) return trimmed;

        var s = trimmed.IndexOf('{');
        var e = trimmed.LastIndexOf('}');
        if (s >= 0 && e > s) return trimmed.Substring(s, e - s + 1);

        return trimmed;
    }
}