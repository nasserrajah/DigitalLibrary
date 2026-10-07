using Library.Domain.Common;
using Library.Domain.Enums;

namespace Library.Domain.Entities;

public class UserLibrary : AuditableEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public long FileSize { get; set; }
    public LibraryBookStatus Status { get; set; } = LibraryBookStatus.Unread;
    public bool IsImported { get; set; }              // imported from user's device
    public string? LocalStorageKey { get; set; }      // server-side reference (if uploaded)
    public string? OriginalFileName { get; set; }
}