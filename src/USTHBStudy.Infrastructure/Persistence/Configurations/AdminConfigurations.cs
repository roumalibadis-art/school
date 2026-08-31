namespace USTHBStudy.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USTHBStudy.Domain.Admin;
using USTHBStudy.Domain.Contributions;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Identity;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorEmail).HasMaxLength(256);
        b.Property(x => x.Action).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Metadata).HasMaxLength(4000);
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.Action);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Link).HasMaxLength(300);
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContributionConfiguration : IEntityTypeConfiguration<Contribution>
{
    public void Configure(EntityTypeBuilder<Contribution> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.FileStorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(120).IsRequired();
        b.Property(x => x.FileHashSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReviewNote).HasMaxLength(2000);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.SubmittedById);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DocumentReportConfiguration : IEntityTypeConfiguration<DocumentReport>
{
    public void Configure(EntityTypeBuilder<DocumentReport> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.ResolutionNote).HasMaxLength(2000);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.DocumentId);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
