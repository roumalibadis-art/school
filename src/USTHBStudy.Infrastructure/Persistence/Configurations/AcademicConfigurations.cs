namespace USTHBStudy.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Common;

internal static class AcademicEntityConfig
{
    /// <summary>Shared column/index/filter setup for every <see cref="AcademicEntity"/>.</summary>
    public static void Base<T>(EntityTypeBuilder<T> b)
        where T : AcademicEntity
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(140).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> b)
    {
        AcademicEntityConfig.Base(b);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.City).HasMaxLength(120);
        b.Property(x => x.Country).HasMaxLength(80).IsRequired();
    }
}

public sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> b)
    {
        AcademicEntityConfig.Base(b);
        b.Property(x => x.Code).HasMaxLength(40);
        b.HasIndex(x => x.UniversityId);
        b.HasOne(x => x.University).WithMany(x => x.Faculties)
            .HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        AcademicEntityConfig.Base(b);
        b.Property(x => x.Code).HasMaxLength(40);
        b.HasIndex(x => x.FacultyId);
        b.HasOne(x => x.Faculty).WithMany(x => x.Departments)
            .HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AcademicDomainConfiguration : IEntityTypeConfiguration<AcademicDomain>
{
    public void Configure(EntityTypeBuilder<AcademicDomain> b)
    {
        AcademicEntityConfig.Base(b);
        b.ToTable("Domains");
        b.Property(x => x.Code).HasMaxLength(40);
        b.HasIndex(x => x.FacultyId);
        b.HasOne(x => x.Faculty).WithMany(x => x.Domains)
            .HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> b)
    {
        AcademicEntityConfig.Base(b);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.AcademicDomainId);
        b.HasOne(x => x.Department).WithMany(x => x.Specialties)
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AcademicDomain).WithMany(x => x.Specialties)
            .HasForeignKey(x => x.AcademicDomainId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> b)
    {
        AcademicEntityConfig.Base(b);
        b.Property(x => x.ShortName).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.SpecialtyId, x.Order });
        b.HasOne(x => x.Specialty).WithMany(x => x.Levels)
            .HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> b)
    {
        AcademicEntityConfig.Base(b);
        b.Property(x => x.ShortName).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.LevelId, x.Order });
        b.HasOne(x => x.Level).WithMany(x => x.Semesters)
            .HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> b)
    {
        AcademicEntityConfig.Base(b);
        b.HasIndex(x => x.StartYear).IsUnique();
        b.HasIndex(x => x.IsCurrent);
    }
}

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        AcademicEntityConfig.Base(b);
        b.HasIndex(x => x.Kind);
    }
}

public sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> b)
    {
        AcademicEntityConfig.Base(b);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Coefficient).HasPrecision(4, 2);
        b.HasIndex(x => new { x.SpecialtyId, x.SemesterId });
        b.HasIndex(x => x.SemesterId);
        b.HasOne(x => x.Semester).WithMany(x => x.Modules)
            .HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Specialty).WithMany(x => x.Modules)
            .HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
    }
}
