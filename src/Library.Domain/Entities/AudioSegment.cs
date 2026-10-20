using Library.Domain.Common;
using Library.Domain.Enums;

namespace Library.Domain.Entities;

public class AudioSegment : AuditableEntity
{
    public Guid BookId { get; set; }
    public Book? Book { get; set; }

    /// <summary>رقم الصفحة في ملف PDF المصدر (0 إن لم يكن من PDF).</summary>
    public int PageNumber { get; set; }

    /// <summary>ترتيب المقطع داخل الكتاب (تصاعدي، فريد لكل كتاب).</summary>
    public int SegmentIndex { get; set; }

    /// <summary>النص الأصلي للمقطع — مصدر إعادة التوليد.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>رمز اللغة: "ar" أو "en".</summary>
    public string Language { get; set; } = "ar";

    /// <summary>اسم الصوت المستخدم عند التوليد (nullable — قد يكون المحلي على العميل).</summary>
    public string? VoiceName { get; set; }

    /// <summary>مفتاح الملف الصوتي في IAudioStorageService (ليس مسارًا ماديًا).</summary>
    public string? AudioFileName { get; set; }

    /// <summary>مدة الصوت بالثواني (nullable — تُملأ بعد التوليد).</summary>
    public double? DurationSeconds { get; set; }

    public AudioSegmentStatus Status { get; set; } = AudioSegmentStatus.Pending;
}