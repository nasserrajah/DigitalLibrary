using Library.Domain.Common;
using Library.Domain.Constants;
using Library.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Library.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImage { get; set; }
    public AuthProvider Provider { get; set; } = AuthProvider.Local;
    public string? ExternalProviderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public long StorageUsed { get; set; }
    public long StorageLimit { get; set; } = StorageConstants.UserStorageLimitBytes;

    public ICollection<UserLibrary> Library { get; set; } = new List<UserLibrary>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public bool HasStorageFor(long bytes) => StorageUsed + bytes <= StorageLimit;
}