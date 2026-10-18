namespace Library.Application.Services;

public class ParsedQuery
{
    public List<string> Categories { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public string CleanQuestion { get; set; } = string.Empty;
}

public static class AiQueryParser
{
    // 🗂️ خريطة التصنيفات
    private static readonly Dictionary<string, string[]> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["رواية"] = new[] { "Fiction", "Novels" },
        ["روايات"] = new[] { "Fiction", "Novels" },
        ["قصص"] = new[] { "Fiction", "Novels" },
        ["قصة"] = new[] { "Fiction", "Novels" },
        ["خيال"] = new[] { "Fiction" },
        ["برمجة"] = new[] { "Programming" },
        ["برمجيات"] = new[] { "Programming" },
        ["كود"] = new[] { "Programming" },
        ["تقنية"] = new[] { "Technology" },
        ["تكنولوجيا"] = new[] { "Technology" },
        ["علوم"] = new[] { "Science" },
        ["علم"] = new[] { "Science" },
        ["رياضيات"] = new[] { "Mathematics" },
        ["تاريخ"] = new[] { "History" },
        ["أدب"] = new[] { "Literature" },
        ["أدبي"] = new[] { "Literature" },
        ["شعر"] = new[] { "Literature" },
        ["تعليم"] = new[] { "Education" },
        ["أعمال"] = new[] { "Business" },
        ["تجارة"] = new[] { "Business" },
        ["اقتصاد"] = new[] { "Business" },
        ["تطوير"] = new[] { "Self Development" },
        ["تطوير ذات"] = new[] { "Self Development" },
        ["تنمية"] = new[] { "Self Development" },
        ["نفسي"] = new[] { "Psychology" },
        ["نفس"] = new[] { "Psychology" },
        ["سيكولوجيا"] = new[] { "Psychology" },
        ["لغات"] = new[] { "Languages" },
        ["لغة"] = new[] { "Languages" },
        ["دين"] = new[] { "Religion" },
        ["ديني"] = new[] { "Religion" },
        ["صحة"] = new[] { "Health" },
        ["طب"] = new[] { "Health" },
        ["رياضة"] = new[] { "Sports" },
        ["أطفال"] = new[] { "Children" },
        ["طفل"] = new[] { "Children" },
    };

    // 🌐 خريطة اللغات
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["عربي"] = "ar", ["عربية"] = "ar", ["العربية"] = "ar",
        ["انجليزي"] = "en", ["انجليزية"] = "en", ["إنجليزي"] = "en",
        ["إنجليزية"] = "en", ["الإنجليزية"] = "en",
        ["فرنسي"] = "fr", ["فرنسية"] = "fr",
        ["ألماني"] = "de", ["ألمانية"] = "de",
        ["صيني"] = "zh", ["صينية"] = "zh",
        ["ياباني"] = "ja", ["يابانية"] = "ja",
        ["اسباني"] = "es", ["إسباني"] = "es",
    };

    // 🛑 كلمات يجب تجاهلها
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ما", "ماذا", "هل", "كيف", "من", "أين", "متى", "لماذا",
        "هي", "هو", "هم", "في", "إلى", "على", "عن", "مع",
        "أريد", "أبحث", "ابحث", "أرغب", "أحتاج", "أعطني", "اقترح", "رشح",
        "لي", "لكم", "عندكم", "لديكم", "يوجد", "توجد", "لدي", "لنا",
        "كتاب", "كتب", "رواية", "روايات", "مؤلف", "مؤلفين", "كاتب",
        "المكتبة", "مكتبة", "قسم", "أي", "أفضل", "أشهر", "جيد", "ممتاز",
        "عنوان", "اسم", "أذكر", "أخبرني", "قل", "معلومات", "تفاصيل",
        "the", "a", "an", "book", "books", "is", "are", "what", "i",
        "want", "find", "search", "author", "novel", "novels", "should",
        "read", "recommend", "suggest", "you", "have", "there", "do",
        "please", "give", "show", "me", "my",
    };

    public static ParsedQuery Parse(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new ParsedQuery();

        return new ParsedQuery
        {
            Categories = ExtractCategories(question),
            Languages = ExtractLanguages(question),
            Keywords = ExtractKeywords(question),
            CleanQuestion = question.Trim(),
        };
    }

    private static List<string> ExtractCategories(string question)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in CategoryMap)
        {
            if (question.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var cat in kvp.Value) found.Add(cat);
            }
        }
        return found.ToList();
    }

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

    private static List<string> ExtractKeywords(string question)
    {
        return question
            .Split(new[] { ' ', '،', ',', '.', '?', '!', '؟', '«', '»', '"', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Where(w => !Stopwords.Contains(w))
            .Where(w => w.Any(char.IsLetter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}