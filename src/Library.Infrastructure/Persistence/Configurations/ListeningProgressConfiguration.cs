using Library.Domain.Entities;
using Library.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class ListeningProgressConfiguration : IEntityTypeConfiguration<ListeningProgress>
{
    public void Configure(EntityTypeBuilder<ListeningProgress> b)
    {
        b.ToTable("ListeningProgresses");
        b.HasKey(x => x.Id);

        // ربط UserId بجدول AspNetUsers دون navigation على الكيان (حفاظًا على الحدود المعمارية)
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // فهرس فريد مركّب: سجل واحد لكل (مستخدم، كتاب)
        b.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
    }
}