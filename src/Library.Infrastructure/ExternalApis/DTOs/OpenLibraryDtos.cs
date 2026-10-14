using System.Text.Json.Serialization;

namespace Library.Infrastructure.ExternalApis.DTOs;

// استجابة /search.json
public class OpenLibrarySearchResponse
{
    [JsonPropertyName("numFound")]
    public int NumFound { get; set; }

    [JsonPropertyName("docs")]
    public List<OpenLibrarySearchDoc> Docs { get; set; } = new();
}

public class OpenLibrarySearchDoc
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author_name")]
    public List<string> AuthorName { get; set; } = new();

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }

    [JsonPropertyName("cover_i")]
    public int? CoverId { get; set; }

    [JsonPropertyName("isbn")]
    public List<string> Isbn { get; set; } = new();

    [JsonPropertyName("subject")]
    public List<string> Subject { get; set; } = new();

    [JsonPropertyName("publisher")]
    public List<string> Publisher { get; set; } = new();

    [JsonPropertyName("language")]
    public List<string> Language { get; set; } = new();

    [JsonPropertyName("number_of_pages_median")]
    public int? NumberOfPagesMedian { get; set; }
}

// استجابة /subjects/{subject}.json
public class OpenLibrarySubjectResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("work_count")]
    public int WorkCount { get; set; }

    [JsonPropertyName("works")]
    public List<OpenLibrarySubjectWork> Works { get; set; } = new();
}

public class OpenLibrarySubjectWork
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public List<OpenLibrarySubjectAuthor> Authors { get; set; } = new();

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }

    [JsonPropertyName("cover_id")]
    public int? CoverId { get; set; }

    [JsonPropertyName("subject")]
    public List<string> Subject { get; set; } = new();

    [JsonPropertyName("edition_count")]
    public int EditionCount { get; set; }
}

public class OpenLibrarySubjectAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string? Key { get; set; }
}