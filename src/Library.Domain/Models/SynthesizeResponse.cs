namespace Library.Domain.Models;

public sealed record SynthesizeResponse
{
    public bool Synthesized { get; init; }
    public string? Provider { get; init; }
    public string? AudioUrl { get; init; }
    public double? DurationSeconds { get; init; }
    public string? Message { get; init; }
}