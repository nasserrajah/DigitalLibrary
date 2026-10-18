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

        var prompt = $$"""
            أنت مساعد مكتبة صارم. مهمتك الإجابة فقط بناءً على الكتب المتاحة.
            اسم المستخدم: "{{userName}}".

            سؤال المستخدم:
            "{{question}}"

            الكتب المتاحة في المكتبة (JSON) — هذه هي القائمة الوحيدة:
            {{booksJson}}

            ══════════════════════════════════════════════
            ⛔ قواعد صارمة — أي تجاوز سيؤدي لرفض ردك:
            ══════════════════════════════════════════════
            1. **لا تذكر أي كتاب غير موجود في القائمة أعلاه** — حتى لو كنت تعرفه.
            2. **لا تخترع مؤلفين، تواريخ، تصنيفات، أو معلومات** — استخدم فقط ما في JSON.
            3. إذا سُئلت عن كتاب **غير موجود** → رد بـ "not_found" في intent.
            4. إذا سُئلت عن كتاب **موجود** → رد بـ "found" واذكر تفاصيله من JSON.
            5. إذا طلب ترشيحات → رشّح من القائمة فقط، وضع keys في mentionedBookKeys.
            6. إذا لم تجد ما يطلبه → اعتذر بلطف واجعل mentionedBookKeys فارغة.

            ══════════════════════════════════════════════
            📋 أعد JSON بهذا الشكل فقط (بدون نص خارج JSON):
            ══════════════════════════════════════════════

            {
              "intent": "found" | "not_found" | "recommendation",
              "reply": "نص الإجابة بالعربية",
              "mentionedBookKeys": ["key1", "key2"]
            }

            قواعد mentionedBookKeys:
            - إذا intent = "not_found" → يجب أن تكون []
            - إذا intent = "found" أو "recommendation" → فقط الـ Keys من القائمة أعلاه
            """;

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                temperature = 0.1,  // 🆕 منخفض جداً = أقل عشوائية
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

            // 🆕 التحقق الصارم
            var invalidKeys = mentionedKeys.Where(k => !validKeys.Contains(k)).ToList();

            if (invalidKeys.Any())
            {
                _logger.LogWarning(
                    "⚠️ AI hallucinated {Count} keys: {Keys}. Rejecting.",
                    invalidKeys.Count, string.Join(", ", invalidKeys));

                // رفض كامل — لا نمرر النص المُخترع
                return $"عذراً {userName}، لم أجد كتباً مطابقة لطلبك في المكتبة حالياً.";
            }

            // 🆕 إذا AI قال "not_found" لكن ذكر كتباً → تناقض → رفض
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