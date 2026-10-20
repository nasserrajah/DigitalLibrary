namespace Library.Domain.Models;

/// <summary>طلب تحويل نص إلى صوت.</summary>
public sealed record TtsRequest(
    string Text,
    string Language = "ar",
    string? VoiceName = null,
    double RateMultiplier = 1.0
);