namespace Library.Application.Services;

public enum AiIntent
{
    Lookup,       // يبحث عن كتاب/مؤلف معين
    Recommend,    // يطلب ترشيح
    General,      // سؤال عام (تحية، ماذا تفعل، إلخ)
}

public class ParsedQuery
{
    public AiIntent Intent { get; set; } = AiIntent.General;
    public List<string> Categories { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
}

public static class AiQueryParser
{
    // ═══════════════════════════════════════════════════════
    // 🗂️ خريطة التصنيفات: عربي → أسماء DB بالإنجليزية
    // ═══════════════════════════════════════════════════════
    private static readonly Dictionary<string, string[]> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // روايات / قصص
        ["رواية"] = new[] { "Fiction", "Novels" },
        ["روايات"] = new[] { "Fiction", "Novels" },
        ["قصص"] = new[] { "Fiction", "Novels" },
        ["قصة"] = new[] { "Fiction", "Novels" },

        // برمجة / تقنية
        ["برمجة"] = new[] { "Programming" },
        ["برمجيات"] = new[] { "Programming" },
        ["تقنية"] = new[] { "Technology" },
        ["تكنولوجيا"] = new[] { "Technology" },

        // علوم / رياضيات
        ["علوم"] = new[] { "Science" },
        ["علم"] = new[] { "Science" },
        ["رياضيات"] = new[] { "Mathematics" },
        ["حساب"] = new[] { "Mathematics" },

        // تاريخ / أدب
        ["تاريخ"] = new[] { "History" },
        ["أدب"] = new[] { "Literature" },
        ["أدبي"] = new[] { "Literature" },
        ["شعر"] = new[] { "Literature" },

        // تعليم / أعمال
        ["تعليم"] = new[] { "Education" },
        ["أعمال"] = new[] { "Business" },
        ["تجارة"] = new[] { "Business" },
        ["اقتصاد"] = new[] { "Business" },

        // تطوير ذات / نفس
        ["تطوير"] = new[] { "Self Development" },
        ["تطوير ذات"] = new[] { "Self Development" },
        ["نفسي"] = new[] { "Psychology" },
        ["نفس"] = new[] { "Psychology" },
        ["سيكولوجيا"] = new[] { "Psychology" },

        // لغات / دين / صحة / رياضة
        ["لغات"] = new[] { "Languages" },
        ["لغة"] = new[] { "Languages" },
        ["دين"] = new[] { "Religion" },
        ["ديني"] = new[] { "Religion" },
        ["صحة"] = new[] { "Health" },
        ["طب"] = new[] { "Health" },
        ["رياضة"] = new[] { "Sports" },

        // أطفال
        ["أطفال"] = new[] { "Children" },
        ["طفل"] = new[] { "Children" },
    };

    // ═══════════════════════════════════════════════════════
    // 🌐 خريطة اللغات: عربي → ISO code
    // ═══════════════════════════════════════════════════════
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["عربي"] = "ar",
        ["عربية"] = "ar",
        ["العربية"] = "ar",
        ["انجليزي"] = "en",
        ["انجليزية"] = "en",
        ["إنجليزي"] = "en",
        ["إنجليزية"] = "en",
        ["الإنجليزية"] = "en",
        ["فرنسي"] = "fr",
        ["فرنسية"] = "fr",
        ["ألماني"] = "de",
        ["ألمانية"] = "de",
        ["صيني"] = "zh",
        ["صينية"] = "zh",
        ["ياباني"] = "ja",
        ["يابانية"] = "ja",
        ["اسباني"] = "es",
        ["إسباني"] = "es",
    };

    // ═══════════════════════════════════════════════════════
    // 🔍 كلمات تدل على "ترشيح"
    // ═══════════════════════════════════════════════════════
    private static readonly string[] RecommendKeywords =
    {
        "اقترح", "رشح", "اختار", "أعطني", "أريد قراءة", "أريد أن أقرأ",
        "ماذا أقرأ", "ساعدني في اختيار", "أفضل", "أنصحني", "دلني",
        "recommend", "suggest", "what should i read"
    };

    // ═══════════════════════════════════════════════════════
    // 🔎 كلمات تدل على "بحث عن كتاب محدد"
    // ═══════════════════════════════════════════════════════
    private static readonly string[] LookupKeywords =
    {
        "هل يوجد", "هل عندكم", "هل توجد", "هل لديكم", "هل هو موجود",
        "أبحث عن", "ابحث عن", "أريد كتاب", "أريد رواية", "دلني على",
        "من مؤلف", "من كتب", "أين أجد", "عندكم كتاب", "يوجد كتاب",
        "do you have", "is there", "find book", "look for"
    };

    // ═══════════════════════════════════════════════════════
    // 🛑 كلمات يجب تجاهلها عند استخراج الكلمات المفتاحية
    // ═══════════════════════════════════════════════════════
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ما", "ماذا", "هل", "كيف", "من", "أين", "متى", "لماذا",
        "هي", "هو", "هم", "في", "من", "إلى", "على", "عن", "مع",
        "أريد", "أبحث", "ابحث", "أرغب", "أحتاج", "أعطني", "اقترح", "رشح",
        "لي", "لكم", "عندكم", "لديكم", "يوجد", "توجد", "لدي",
        "كتاب", "كتب", "رواية", "روايات", "مؤلف", "مؤلفين", "كاتب",
        "المكتبة", "مكتبة", "قسم", "أي", "أفضل", "أشهر", "جيد", "ممتاز",
        "the", "a", "an", "book", "books", "is", "are", "what", "i",
        "want", "find", "search", "author", "novel", "novels", "should",
        "read", "recommend", "suggest", "you", "have", "there", "do",
    };

    // ═══════════════════════════════════════════════════════
    // 🎯 الدالة الرئيسية
    // ═══════════════════════════════════════════════════════
    public static ParsedQuery Parse(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new ParsedQuery { Intent = AiIntent.General };

        var normalized = NormalizeArabic(question);

        var result = new ParsedQuery
        {
            Categories = ExtractCategories(normalized),
            Languages = ExtractLanguages(normalized),
            Keywords = ExtractKeywords(normalized),
        };

        // تحديد النية
        if (ContainsAny(normalized, RecommendKeywords))
        {
            result.Intent = AiIntent.Recommend;
        }
        else if (ContainsAny(normalized, LookupKeywords))
        {
            result.Intent = AiIntent.Lookup;
        }
        else if (result.Categories.Any() || result.Languages.Any())
        {
            // لم يقل "اقترح" لكن ذكر تصنيف/لغة → افتراضياً يريد ترشيح
            result.Intent = AiIntent.Recommend;
        }
        else if (result.Keywords.Any())
        {
            // لديه كلمات مفتاحية لكن لا نية واضحة → معاملة كبحث
            result.Intent = AiIntent.Lookup;
        }
        else
        {
            result.Intent = AiIntent.General;
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════
    // 📂 استخراج التصنيفات
    // ═══════════════════════════════════════════════════════
    private static List<string> ExtractCategories(string question)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in CategoryMap)
        {
            if (question.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var cat in kvp.Value)
                    found.Add(cat);
            }
        }

        return found.ToList();
    }

    // ═══════════════════════════════════════════════════════
    // 🌐 استخراج اللغات
    // ═══════════════════════════════════════════════════════
    private static List<string> ExtractLanguages(string question)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in LanguageMap)
        {
            if (question.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                found.Add(kvp.Value);
        }

        return found.ToList();
    }

    // ═══════════════════════════════════════════════════════
    // 🔤 استخراج الكلمات المفتاحية (للبحث عن كتاب محدد)
    // ═══════════════════════════════════════════════════════
    private static List<string> ExtractKeywords(string question)
    {
        var words = question
            .Split(new[] { ' ', '،', ',', '.', '?', '!', '؟', '«', '»', '"', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Where(w => !Stopwords.Contains(w))
            .Where(w => w.Any(char.IsLetter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return words;
    }

    // ═══════════════════════════════════════════════════════
    // 🧹 تطبيع النص العربي
    // ═══════════════════════════════════════════════════════
    private static string NormalizeArabic(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("أ", "ا")
            .Replace("إ", "ا")
            .Replace("آ", "ا")
            .Replace("ى", "ي")
            .Replace("ة", "ه")
            .Replace("ؤ", "و")
            .Replace("ئ", "ي");
    }

    private static bool ContainsAny(string text, string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}