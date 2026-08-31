namespace USTHBStudy.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Infrastructure.Identity;
using USTHBStudy.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core context. Owns the ASP.NET Core Identity schema plus application tables.
/// Entity configurations live in <c>Persistence/Configurations</c> and are applied by assembly scan.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
