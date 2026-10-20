namespace Library.Domain.Models;

public sealed record ListeningProgressDto
{
    public Guid BookId { get; init; }
    public int LastSegmentIndex { get; init; }
    public double LastPositionSeconds { get; init; }
    public DateTime UpdatedAt { get; init; }
}