using Library.Domain.Common;

namespace Library.Domain.Entities;

public class Category : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Book> Books { get; set; } = new List<Book>();
}