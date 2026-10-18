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
        bool isFirstMessage = true,
        string historyContext = "",
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

        // ═══════════════════════════════════════════════
        // 🆕 القاعدة الذهبية: ترحيب فقط في أول رسالة
        // ═══════════════════════════════════════════════
        var greetingRule = isFirstMessage
            ? $"""
               ✅ هذه أول رسالة في المحادثة.
               → ابدأ ردك بتحية واحدة فقط: "أهلاً {userName}! 👋" أو "مرحباً {userName}!"
               → بعد التحية، أجب على السؤال مباشرة.
               """
            : """
               ❌ هذه ليست أول رسالة — المستخدم يعرفك بالفعل.
               → **لا تبدأ بـ** "أهلاً" أو "مرحباً" أو "أهلاً بك" أو أي تحية
               → ابدأ مباشرة بالإجابة على السؤال
               → كن طبيعياً كأنك تكمل محادثة مستمرة
               """;

        // ═══════════════════════════════════════════════
        // 🆕 قسم السياق (فقط إذا لم تكن أول رسالة)
        // ═══════════════════════════════════════════════
        var historySection = !string.IsNullOrWhiteSpace(historyContext)
            ? $$"""

              ═══════════════════════════════════════════════════════
              💬 سياق المحادثة السابقة (للفهم فقط، لا تعيد صياغته):
              ═══════════════════════════════════════════════════════
              {{historyContext}}
              """
            : "";

        var prompt = $$"""
            أنت "سند" — المساعد الذكي في مكتبة Smart Library.
            أنت ودود، ذكي، وتتحدث بأسلوب طبيعي كأنك صديق قارئ.
            اسم المستخدم: "{{userName}}".

            ═══════════════════════════════════════════════════════
            🎯 حالة المحادثة (اقرأ بعناية):
            ═══════════════════════════════════════════════════════
            {{greetingRule}}
            {{historySection}}

            ═══════════════════════════════════════════════════════
            📚 الكتب المتاحة في المكتبة (JSON):
            ═══════════════════════════════════════════════════════
            {{booksJson}}

            ═══════════════════════════════════════════════════════
            ❓ سؤال المستخدم الحالي:
            ═══════════════════════════════════════════════════════
            "{{question}}"

            ═══════════════════════════════════════════════════════
            🎯 قواعد الإجابة (بدون استثناء):
            ═══════════════════════════════════════════════════════

            ✅ السيناريو 1: يسأل عن كتاب **موجود**
               → قل نعم بثقة
               → اذكر: العنوان، المؤلف، التصنيف، اللغة، السنة
               → مثال: "نعم، «الرهائن الثلاث» متوفر! للمؤلف جون بوكان، قسم الأدب"

            ✅ السيناريو 2: يسأل عن كتاب **غير موجود**
               → اعتذر بلطف
               → قل "قريباً سنضيفه"
               → اقترح 2-3 كتب مشابهة من القائمة
               → مثال: "عذراً، Harry Potter غير متوفر حالياً، لكننا نعمل على إضافته قريباً 📚"

            ✅ السيناريو 3: مؤلف
               → اذكر كتبه من القائمة، أو اعتذر بلطف

            ✅ السيناريو 4: تصنيف
               → اذكر عدد الكتب + 3-5 عناوين منها

            ✅ السيناريو 5: ترشيح
               → رشّح من القائمة الفعلية فقط

            ✅ السيناريو 6: تحية أو سؤال عام
               → رد بشكل ودود ومختصر

            ═══════════════════════════════════════════════════════
            ⛔ محاذير صارمة (لن يُقبل ردك إن خالفتها):
            ═══════════════════════════════════════════════════════
            ❌ لا تذكر أي كتاب غير موجود في القائمة أعلاه
            ❌ لا تخترع مؤلفين، سنوات، أو تفاصيل
            ❌ لا تكرر التحية إذا لم تكن أول رسالة (هذا أهم شرط!)
            ❌ لا تعطي إجابات طويلة (2-4 أسطر كافية)
            ❌ لا تستخدم كلمات رسمية مثل "بصفتي مساعداً"

            ═══════════════════════════════════════════════════════
            📋 صيغة الرد (JSON فقط):
            ═══════════════════════════════════════════════════════
            {
              "reply": "نص الإجابة بالعربية — طبيعي وودود",
              "mentionedBookKeys": ["key1", "key2"],
              "intent": "book_found" | "book_not_found" | "recommendation" | "greeting" | "info" | "category" | "author"
            }

            ═══════════════════════════════════════════════════════
            📚 أمثلة (تعلم أسلوب الرد):
            ═══════════════════════════════════════════════════════

            🟢 أمثلة على **أول رسالة** (فيها تحية):
            ────────────────────────────────────────────
            User: "مرحبا"
            Reply: {"reply": "أهلاً {{userName}}! 👋 أنا سند، كيف أقدر أساعدك اليوم؟", "mentionedBookKeys": [], "intent": "greeting"}

            User: "هل عندكم كتاب الرهائن الثلاث؟"
            Reply: {"reply": "أهلاً {{userName}}! 📖 نعم، «الرهائن الثلاث» متوفر — للمؤلف جون بوكان، قسم الأدب.", "mentionedBookKeys": ["<real-key>"], "intent": "book_found"}

            🔴 أمثلة على **رسائل متابعة** (بدون تحية):
            ────────────────────────────────────────────
            User: "من مؤلفه؟"
            Reply: {"reply": "المؤلف هو جون بوكان 🖊️", "mentionedBookKeys": [], "intent": "author"}

            User: "اقترح رواية أخرى"
            Reply: {"reply": "أنصحك بـ [رواية] — قصة مميزة جداً تستحق القراءة 📚", "mentionedBookKeys": [], "intent": "recommendation"}

            User: "هل عندكم Harry Potter؟"
            Reply: {"reply": "عذراً، Harry Potter غير متوفر حالياً، لكننا نعمل على إضافته قريباً! لدينا [رواية أخرى] قد تعجبك 📚", "mentionedBookKeys": [], "intent": "book_not_found"}

            الآن أجب على سؤال المستخدم أعلاه بالطريقة المناسبة حسب حالة المحادثة.
            """;

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 800,
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

            var invalidKeys = mentionedKeys.Where(k => !validKeys.Contains(k)).ToList();

            if (invalidKeys.Any())
            {
                _logger.LogWarning(
                    "⚠️ AI hallucinated {Count} keys: {Keys}. Rejecting.",
                    invalidKeys.Count, string.Join(", ", invalidKeys));

                return $"عذراً {userName}، لم أجد كتباً مطابقة لطلبك في المكتبة حالياً.";
            }

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