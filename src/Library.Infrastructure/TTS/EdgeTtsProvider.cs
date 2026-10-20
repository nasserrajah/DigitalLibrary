using EdgeTtsSharp;
using Library.Domain.Interfaces;
using Library.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.TTS;

public sealed class EdgeTtsProvider : ITextToSpeechProvider
{
    private const string DefaultArabicVoice = "ar-YE-SalehNeural";
    private const string DefaultEnglishVoice = "en-US-AriaNeural";
    private static readonly TimeSpan SynthesisTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<EdgeTtsProvider> _log;

    public EdgeTtsProvider(ILogger<EdgeTtsProvider> log) => _log = log;

    public string Name => "EdgeTTS";

    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return TtsResult.Fail(Name, "Text is empty.");

        var voiceName = ResolveVoice(request);
        var tempFile = Path.Combine(Path.GetTempPath(), $"edge_tts_{Guid.NewGuid():N}.mp3");

        try
        {
            _log.LogInformation("EdgeTTS start: voice={Voice}, length={Length}", voiceName, request.Text.Length);

            // EdgeTtsSharp لا يدعم CancellationToken مباشرة، لذا نستخدم مهلة زمنية
            var voice = await EdgeTts.GetVoice(voiceName);
            if (voice == null)
                return TtsResult.Fail(Name, $"Voice '{voiceName}' not found.");

            var saveTask = voice.SaveAudioToFile(request.Text, tempFile);
            var completed = await Task.WhenAny(saveTask, Task.Delay(SynthesisTimeout, ct));

            if (completed != saveTask)
            {
                _log.LogError("EdgeTTS timed out after {Seconds}s", SynthesisTimeout.TotalSeconds);
                return TtsResult.Fail(Name, $"EdgeTTS timed out after {SynthesisTimeout.TotalSeconds:0}s.");
            }

            // انتظار المهمة للتحقق من أي استثناءات
            await saveTask;

            if (!File.Exists(tempFile))
                return TtsResult.Fail(Name, "EdgeTTS did not produce a file.");

            var bytes = await File.ReadAllBytesAsync(tempFile, ct);
            if (bytes.Length == 0)
                return TtsResult.Fail(Name, "EdgeTTS returned empty audio.");

            _log.LogInformation("EdgeTTS success: {Bytes} bytes for {Voice}", bytes.Length, voiceName);
            return TtsResult.Ok(bytes, "audio/mpeg", voiceName, Name);
        }
        catch (OperationCanceledException)
        {
            return TtsResult.Fail(Name, "Synthesis cancelled.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "EdgeTTS failed for voice {Voice}", voiceName);
            return TtsResult.Fail(Name, ex.Message);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
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