namespace USTHBStudy.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Domain.Students;
using USTHBStudy.Domain.Subscriptions;
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

    public DbSet<University> Universities => Set<University>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AcademicDomain> Domains => Set<AcademicDomain>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Module> Modules => Set<Module>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
