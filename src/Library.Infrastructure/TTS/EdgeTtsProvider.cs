using Library.Domain.Interfaces;
using Library.Domain.Models;
using Microsoft.Extensions.Logging;
using EdgeTtsSharp;

namespace Library.Infrastructure.TTS;

/// <summary>
/// مزوّد TTS مجاني بالكامل يستخدم خدمة Edge Read Aloud عبر مكتبة EdgeTtsSharp.
/// لا يتطلب مفتاح API ولا حسابًا. يعيد MP3 (audio/mpeg).
/// الأصوات الافتراضية: ar-YE-SalehNeural (عربي) وen-US-AriaNeural (إنجليزي).
/// </summary>
public sealed class EdgeTtsProvider : ITextToSpeechProvider
{
    private const string DefaultArabicVoice = "ar-YE-SalehNeural";
    private const string DefaultEnglishVoice = "en-US-AriaNeural";

    private readonly ILogger<EdgeTtsProvider> _log;

    public EdgeTtsProvider(ILogger<EdgeTtsProvider> log) => _log = log;

    public string Name => "EdgeTTS";

    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return TtsResult.Fail(Name, "Text is empty.");

        var voiceName = ResolveVoice(request);

        try
        {
            _log.LogInformation(
                "EdgeTTS synthesizing: voice={Voice}, length={Length}",
                voiceName, request.Text.Length);

            // EdgeTtsSharp API: GetVoice → GetAudioStream
            var voice = await EdgeTts.GetVoice(voiceName);

            if (voice is null)
                return TtsResult.Fail(Name, $"Voice '{voiceName}' not found.");

            // GetAudioStream يعيد Stream يمكن القراءة منه
            await using var audioStream = voice.GetAudioStream(request.Text);

            using var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, ct);

            var bytes = ms.ToArray();
            if (bytes.Length == 0)
                return TtsResult.Fail(Name, "EdgeTTS returned empty audio.");

            _log.LogInformation("EdgeTTS produced {Bytes} bytes for voice {Voice}",
                bytes.Length, voiceName);

            return TtsResult.Ok(bytes, "audio/mpeg", voiceName, Name);
        }
        catch (OperationCanceledException)
        {
            return TtsResult.Fail(Name, "Synthesis cancelled.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "EdgeTTS synthesis failed for voice {Voice}", voiceName);
            return TtsResult.Fail(Name, ex.Message);
        }
    }

    private static string ResolveVoice(TtsRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.VoiceName))
            return request.VoiceName.Trim();

        return string.Equals(request.Language, "ar", StringComparison.OrdinalIgnoreCase)
            ? DefaultArabicVoice
            : DefaultEnglishVoice;
    }
}