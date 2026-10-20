namespace Library.Domain.Models;

public sealed record AudioSegmentDto
{
    public Guid Id { get; init; }
    public Guid BookId { get; init; }
    public int PageNumber { get; init; }
    public int SegmentIndex { get; init; }
    public string Text { get; init; } = string.Empty;
    public string Language { get; init; } = "ar";
    public string? VoiceName { get; init; }
    public double? DurationSeconds { get; init; }
    public string Status { get; init; } = "Pending";
    public string? AudioUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}