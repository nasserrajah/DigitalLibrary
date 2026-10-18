namespace Library.Domain.DTOs;

public sealed class AiBookCandidate
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? CategoryName { get; set; }   // 🆕
    public string? Language { get; set; }       // 🆕
    public int? Year { get; set; }              // 🆕
    public string? Format { get; set; }         // 🆕
    public string? Key { get; set; }
}