using Library.Domain.Common;
using Library.Domain.Enums;

namespace Library.Domain.Entities;

public class Book : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public DateTime? PublicationDate { get; set; }
    public string? ISBN { get; set; }
    public string Language { get; set; } = "ar";
    public string? CoverImage { get; set; }

    public string? FileName { get; set; }
    public string? FilePath { get; set; } // storage key, NOT physical path
    public long FileSize { get; set; }
    public BookFormat Format { get; set; } = BookFormat.Pdf;
    public bool IsPublished { get; set; } = true;
    public Guid? CreatedBy { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<UserLibrary> InLibraries { get; set; } = new List<UserLibrary>();
}