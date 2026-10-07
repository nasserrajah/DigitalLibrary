using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
    }
}

public class BookConfig : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> b)
    {
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Author).IsRequired().HasMaxLength(200);
        b.Property(x => x.ISBN).HasMaxLength(20);
        b.HasIndex(x => x.ISBN).IsUnique().HasFilter("[ISBN] IS NOT NULL");
        b.HasIndex(x => x.Title);
        b.HasIndex(x => x.Author);
        b.HasIndex(x => x.CategoryId);

        b.HasOne(x => x.Category)
         .WithMany(c => c.Books)
         .HasForeignKey(x => x.CategoryId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FavoriteConfig : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
        b.HasOne(x => x.Book).WithMany(x => x.Favorites)
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(u => u.Favorites)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserLibraryConfig : IEntityTypeConfiguration<UserLibrary>
{
    public void Configure(EntityTypeBuilder<UserLibrary> b)
    {
        b.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
        b.HasOne(x => x.Book).WithMany(x => x.InLibraries)
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany(u => u.Library)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Message).IsRequired().HasMaxLength(2000);
        b.HasIndex(x => new { x.UserId, x.IsRead });
    }
}

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasIndex(x => x.Token).IsUnique();
        b.Property(x => x.Token).IsRequired().HasMaxLength(500);
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Email).IsUnique();
    }
}