namespace USTHBStudy.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Infrastructure.Identity;

public sealed class ClassificationSettingsConfiguration : IEntityTypeConfiguration<ClassificationSettings>
{
    public void Configure(EntityTypeBuilder<ClassificationSettings> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.RequiredFields).HasConversion<int>();
        b.Property(x => x.NonEducationalPolicy).HasConversion<int>();
    }
}

public sealed class ClassificationTaskConfiguration : IEntityTypeConfiguration<ClassificationTask>
{
    public void Configure(EntityTypeBuilder<ClassificationTask> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Trigger).HasConversion<int>();
        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasIndex(x => x.CreatedAt);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Assignments).WithOne(x => x.Task).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClassificationAssignmentConfiguration : IEntityTypeConfiguration<ClassificationAssignment>
{
    public void Configure(EntityTypeBuilder<ClassificationAssignment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<int>();

        // A user can be handed a document at most once per voting round — database-enforced.
        b.HasIndex(x => new { x.DocumentId, x.UserId, x.Round }).IsUnique();
        // Slot counting for the queue.
        b.HasIndex(x => new { x.DocumentId, x.Round, x.Status });
        b.HasIndex(x => new { x.UserId, x.Status });

        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClassificationVoteConfiguration : IEntityTypeConfiguration<ClassificationVote>
{
    public void Configure(EntityTypeBuilder<ClassificationVote> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Decision).HasConversion<int>();
        b.Property(x => x.DocumentType).HasConversion<int?>();

        // One vote per user per document per round — database-enforced, so concurrent duplicates fail.
        b.HasIndex(x => new { x.DocumentId, x.UserId, x.Round }).IsUnique();
        b.HasIndex(x => new { x.DocumentId, x.Round });
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.SpecialtyProposalId);
        b.HasIndex(x => x.DepartmentProposalId);
        b.HasIndex(x => x.DocumentTypeProposalId);
        b.HasIndex(x => x.AcademicYearProposalId);
        b.HasIndex(x => x.SessionProposalId);

        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Specialty>().WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserContributionStatsConfiguration : IEntityTypeConfiguration<UserContributionStats>
{
    public void Configure(EntityTypeBuilder<UserContributionStats> b)
    {
        b.HasKey(x => x.UserId);
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserContributionStats>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaxonomyProposalConfiguration : IEntityTypeConfiguration<TaxonomyProposal>
{
    public void Configure(EntityTypeBuilder<TaxonomyProposal> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.ResolvedDocumentType).HasConversion<int?>();
        b.Property(x => x.Value).HasMaxLength(160).IsRequired();
        b.Property(x => x.DedupeKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.AdminNote).HasMaxLength(1000);
        b.Property(x => x.ApprovedName).HasMaxLength(160);

        // Same value (accent/case-insensitive) in the same category under the same parent is one proposal.
        b.HasIndex(x => x.DedupeKey).IsUnique();
        b.HasIndex(x => new { x.Status, x.Category, x.SubmittedAt });
        b.HasIndex(x => new { x.SubmittedById, x.Status });

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExternalLoginTicketConfiguration : IEntityTypeConfiguration<ExternalLoginTicket>
{
    public void Configure(EntityTypeBuilder<ExternalLoginTicket> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Purpose).HasConversion<int>();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
