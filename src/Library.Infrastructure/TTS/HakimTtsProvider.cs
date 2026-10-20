using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Library.Domain.Interfaces;
using Library.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Library.Infrastructure.TTS;

public sealed class HakimTtsProvider : ITextToSpeechProvider
{
    // ✅ النقطة الصحيحة من الوثائق الرسمية
    private const string Endpoint = "https://api.tryhakim.ai/v1/audio/speech";
    // الصوت الافتراضي (يمكنك تغييره من لوحة التحكم عبر GET /v1/audio/voices)
    private const string DefaultVoiceId = "cmokbc2b1001pvu39wmj61b7h";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly HttpClient _http;
    private readonly ILogger<HakimTtsProvider> _log;
    private readonly string? _apiKey;

    public HakimTtsProvider(HttpClient http, IConfiguration cfg, ILogger<HakimTtsProvider> log)
    {
        _http = http;
        _log = log;
        _apiKey = cfg["Hakim:ApiKey"];
    }

    public string Name => "HakimTTS";

    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return TtsResult.NotConfigured(Name);

        if (string.IsNullOrWhiteSpace(request.Text))
            return TtsResult.Fail(Name, "Text is empty.");

        var voiceId = string.IsNullOrWhiteSpace(request.VoiceName) ? DefaultVoiceId : request.VoiceName.Trim();

        // ✅ الحقول المطلوبة حسب الوثائق: model, voice, input
        var payload = new
        {
            model = "hakim-fast-v1",
            voice = voiceId,
            input = request.Text
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpReq.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);

        try
        {
            _log.LogInformation("HakimTTS start: voice={Voice}, length={Length}", voiceId, request.Text.Length);

            using var resp = await _http.SendAsync(httpReq, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                _log.LogError("HakimTTS failed {Status}: {Body}", resp.StatusCode, body);
                return TtsResult.Fail(Name, $"Hakim returned {(int)resp.StatusCode}.");
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
            if (bytes.Length == 0)
                return TtsResult.Fail(Name, "Hakim returned empty audio.");

            _log.LogInformation("HakimTTS success: {Bytes} bytes", bytes.Length);
            return TtsResult.Ok(bytes, "audio/mpeg", voiceId, Name);
        }
        catch (OperationCanceledException)
        {
            return TtsResult.Fail(Name, "HakimTTS timed out.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "HakimTTS exception");
            return TtsResult.Fail(Name, ex.Message);
        }
    }
}