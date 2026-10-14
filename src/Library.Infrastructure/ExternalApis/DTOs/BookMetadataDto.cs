namespace Library.Infrastructure.ExternalApis.DTOs;

public class BookMetadataDto
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = "Unknown";
    public string? Description { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public string? ISBN { get; set; }
    public string? Language { get; set; } = "en";
    public string? CoverUrl { get; set; }
    public List<string> Subjects { get; set; } = new();
    public int? NumberOfPages { get; set; }
    public string? Source { get; set; } = "OpenLibrary";
}