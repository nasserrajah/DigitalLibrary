namespace Library.Domain.Models;

/// <summary>
/// نتيجة تحويل النص إلى صوت — مستقلة عن أي مزوّد.
/// </summary>
public sealed record TtsResult
{
    public bool Success { get; init; }
    public byte[]? AudioBytes { get; init; }
    public string? ContentType { get; init; }   // "audio/mpeg", "audio/wav", ...
    public string? VoiceName { get; init; }
    public string? ProviderName { get; init; }
    public string? ErrorMessage { get; init; }

    public static TtsResult Ok(byte[] bytes, string contentType, string? voiceName, string providerName) => new()
    {
        Success = true,
        AudioBytes = bytes,
        ContentType = contentType,
        VoiceName = voiceName,
        ProviderName = providerName
    };

    public static TtsResult NotConfigured(string providerName) => new()
    {
        Success = false,
        ProviderName = providerName,
        ErrorMessage = "TTS provider is not configured."
    };

    public static TtsResult Fail(string providerName, string error) => new()
    {
        Success = false,
        ProviderName = providerName,
        ErrorMessage = error
    };
}