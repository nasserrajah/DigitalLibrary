using Library.Domain.Common;

namespace Library.Domain.Entities;

public class Favorite : BaseEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}