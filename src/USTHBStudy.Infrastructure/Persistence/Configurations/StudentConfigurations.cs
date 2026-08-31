namespace USTHBStudy.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USTHBStudy.Domain.Students;
using USTHBStudy.Infrastructure.Identity;

public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.Kind, x.EntityId }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.Kind, x.OccurredAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.SetNull);
    }
}
