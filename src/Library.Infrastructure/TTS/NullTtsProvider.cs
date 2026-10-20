using Library.Domain.Interfaces;
using Library.Domain.Models;

namespace Library.Infrastructure.TTS;

/// <summary>
/// مزوّد TTS افتراضي "لا يفعل شيئًا".
/// يعيد NotConfigured بهدوء — لا يعطّل التطبيق ولا يُرسل نصًا لأي خدمة خارجية.
/// عند تفعيل مزوّد حقيقي، استبدل التسجيل في DependencyInjection.
/// </summary>
public class NullTtsProvider : ITextToSpeechProvider
{
    public string Name => "Null";

    public Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken ct = default)
        => Task.FromResult(TtsResult.NotConfigured(Name));
}