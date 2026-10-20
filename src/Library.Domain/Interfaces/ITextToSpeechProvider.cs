using Library.Domain.Models;

namespace Library.Domain.Interfaces;

/// <summary>
/// تجريد مزوّد تحويل النص إلى صوت.
/// المزوّد الافتراضي هو NullTtsProvider (لا يُنتج صوتًا، لا يعطّل التطبيق).
/// يمكن إضافة Azure/Google لاحقًا دون تغيير أي مستهلك.
/// </summary>
public interface ITextToSpeechProvider
{
    /// <summary>اسم المزوّد (للتوثيق والتشخيص).</summary>
    string Name { get; }

    /// <summary>
    /// توليد الصوت. التنفيذات يجب أن تُعيد TtsResult.NotConfigured بدلاً من رمي استثناء
    /// عند غياب المفاتيح/التهيئة، حتى لا يتعطّل التطبيق.
    /// </summary>
    Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken ct = default);
}