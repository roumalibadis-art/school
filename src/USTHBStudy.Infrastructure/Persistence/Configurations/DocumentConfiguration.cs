namespace USTHBStudy.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USTHBStudy.Domain.Documents;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.FileStorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.PreviewStorageKey).HasMaxLength(500);
        b.Property(x => x.ThumbnailStorageKey).HasMaxLength(500);
        b.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(120).IsRequired();
        b.Property(x => x.FileHashSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.Source).HasMaxLength(500);
        b.Property(x => x.PermissionNotes).HasMaxLength(1000);
        b.Property(x => x.ReviewNote).HasMaxLength(2000);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.FileHashSha256);
        b.HasIndex(x => new { x.Status, x.IsPremium, x.CreatedAt });
        b.HasIndex(x => new { x.ModuleId, x.Type, x.Status });
        b.HasIndex(x => new { x.AcademicYearId, x.Type });

        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasOne(x => x.Module).WithMany()
            .HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Session).WithMany()
            .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.SolutionForDocument).WithMany(x => x.Solutions)
            .HasForeignKey(x => x.SolutionForDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
