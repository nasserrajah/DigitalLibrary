using System.Text.Json.Serialization;

namespace Library.Infrastructure.ExternalApis.DTOs;

public class GutendexResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("results")]
    public List<GutendexBook> Results { get; set; } = new();
}

public class GutendexBook
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public List<GutendexAuthor> Authors { get; set; } = new();

    [JsonPropertyName("summaries")]
    public List<string> Summaries { get; set; } = new();

    [JsonPropertyName("subjects")]
    public List<string> Subjects { get; set; } = new();

    [JsonPropertyName("bookshelves")]
    public List<string> Bookshelves { get; set; } = new();

    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = new();

    [JsonPropertyName("download_count")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("formats")]
    public Dictionary<string, string> Formats { get; set; } = new();
}

public class GutendexAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("birth_year")]
    public int? BirthYear { get; set; }

    [JsonPropertyName("death_year")]
    public int? DeathYear { get; set; }
}