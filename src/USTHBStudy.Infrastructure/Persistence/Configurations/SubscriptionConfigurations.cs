namespace USTHBStudy.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using USTHBStudy.Domain.Subscriptions;
using USTHBStudy.Infrastructure.Identity;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(140).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Features).HasMaxLength(4000);
        b.Property(x => x.Price).HasPrecision(12, 2);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.DisplayOrder });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.PriceAtPurchase).HasPrecision(12, 2);

        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasIndex(x => x.EndsAt);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne(p => p.Subscription!).HasForeignKey(p => p.SubscriptionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        b.Property(x => x.TransactionReference).HasMaxLength(64).IsRequired();
        b.Property(x => x.AdminNote).HasMaxLength(2000);
        b.Property(x => x.Amount).HasPrecision(12, 2);

        b.HasIndex(x => x.TransactionReference).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.UserId);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
