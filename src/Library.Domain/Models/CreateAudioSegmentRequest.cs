namespace Library.Domain.Models;

public sealed record CreateAudioSegmentRequest(
    int SegmentIndex,
    int PageNumber,
    string Text,
    string Language = "ar"
);