using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class AudioSegmentConfiguration : IEntityTypeConfiguration<AudioSegment>
{
    public void Configure(EntityTypeBuilder<AudioSegment> b)
    {
        b.ToTable("AudioSegments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(8000);   // حد أمان لكل مقطع

        b.Property(x => x.Language)
            .IsRequired()
            .HasMaxLength(10);

        b.Property(x => x.VoiceName)
            .HasMaxLength(150);

        b.Property(x => x.AudioFileName)
            .HasMaxLength(300);

        b.Property(x => x.Status)
            .HasConversion<int>();

        b.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // منع تكرار نفس الترتيب لنفس الكتاب
        b.HasIndex(x => new { x.BookId, x.SegmentIndex }).IsUnique();
    }
}