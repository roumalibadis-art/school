namespace USTHBStudy.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Infrastructure.Identity;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StudentId).HasMaxLength(50);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.PremiumExpiresAt);

        // Academic profile (PRD §9/§19). Optional; RESTRICT because academic entities are soft-deleted.
        builder.HasOne<University>().WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Specialty>().WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Level>().WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict);
    }
}
