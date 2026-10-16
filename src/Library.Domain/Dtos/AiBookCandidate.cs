namespace Library.Domain.Dtos;

public sealed class AiBookCandidate
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }
    public int? Year { get; set; }
    public string? Key { get; set; }
}