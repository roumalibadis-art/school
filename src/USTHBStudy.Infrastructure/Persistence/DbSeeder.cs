namespace USTHBStudy.Infrastructure.Persistence;

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Application.Common;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Subscriptions;
using USTHBStudy.Infrastructure.Identity;

/// <summary>
/// Idempotent seeding (PRD §52). Roles and their permission claims are always ensured;
/// demo accounts are seeded only when explicitly enabled (Development).
/// </summary>
public sealed class DbSeeder
{
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        RoleManager<ApplicationRole> roles,
        UserManager<ApplicationUser> users,
        AppDbContext db,
        ILogger<DbSeeder> logger)
    {
        _roles = roles;
        _users = users;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Roles and permission claims are always ensured. <paramref name="includeDemoUsers"/> and
    /// <paramref name="includeSampleAcademicData"/> are Development conveniences (PRD §52); production
    /// keeps academic data admin-managed (PRD §80).
    /// </summary>
    public async Task SeedAsync(
        bool includeDemoUsers,
        bool includeSampleAcademicData = false,
        CancellationToken ct = default)
    {
        await SeedRolesAndPermissionsAsync();

        if (includeDemoUsers)
        {
            await SeedDemoUsersAsync();
        }

        if (includeSampleAcademicData)
        {
            await SeedAcademicSampleAsync(ct);
            await SeedSubscriptionPlansAsync(ct);
        }
    }

    private async Task SeedSubscriptionPlansAsync(CancellationToken ct)
    {
        if (await _db.SubscriptionPlans.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var features = "Tous les documents Premium\nCorrigés Premium\nSans engagement";
        _db.SubscriptionPlans.AddRange(
            new SubscriptionPlan { Name = "1 mois", Slug = "1-mois", DurationDays = 30, Price = 500m, Currency = "DZD", Features = features, DisplayOrder = 1 },
            new SubscriptionPlan { Name = "3 mois", Slug = "3-mois", DurationDays = 90, Price = 1200m, Currency = "DZD", Features = features, DisplayOrder = 2 },
            new SubscriptionPlan { Name = "6 mois", Slug = "6-mois", DurationDays = 180, Price = 2000m, Currency = "DZD", Features = features, DisplayOrder = 3 },
            new SubscriptionPlan { Name = "Année universitaire", Slug = "annee-universitaire", DurationDays = 300, Price = 3000m, Currency = "DZD", Features = features, DisplayOrder = 4 });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 4 sample subscription plans.");
    }

    private async Task SeedRolesAndPermissionsAsync()
    {
        foreach (var (roleName, permissions) in Permissions.DefaultRoleGrants)
        {
            var role = await _roles.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ApplicationRole(roleName) { Description = $"{roleName} role" };
                var created = await _roles.CreateAsync(role);
                if (!created.Succeeded)
                {
                    _logger.LogError(
                        "Failed to create role {Role}: {Errors}",
                        roleName,
                        string.Join(", ", created.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            var existing = (await _roles.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permission in permissions.Where(p => !existing.Contains(p)))
            {
                await _roles.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
            }
        }
    }

    private async Task SeedDemoUsersAsync()
    {
        await EnsureUserAsync("admin@example.local", "Amine", "Admin", "Admin#2026!", Roles.Admin);
        await EnsureUserAsync("moderator@example.local", "Mira", "Moderator", "Moderator#2026!", Roles.Moderator);
        await EnsureUserAsync("student@example.local", "Sami", "Student", "Student#2026!", Roles.Student);
    }

    private async Task EnsureUserAsync(string email, string firstName, string lastName, string password, string role)
    {
        if (await _users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
            IsActive = true,
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            _logger.LogWarning(
                "Failed to seed demo user {Email}: {Errors}",
                email,
                string.Join(", ", created.Errors.Select(e => e.Description)));
            return;
        }

        await _users.AddToRoleAsync(user, role);
        _logger.LogInformation("Seeded demo {Role} account {Email}", role, email);
    }

    private async Task SeedAcademicSampleAsync(CancellationToken ct)
    {
        if (await _db.Universities.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var university = new University { Name = "Université des Sciences et de la Technologie Houari Boumediene", Code = "USTHB", City = "Alger" };
        var faculty = new Faculty { Name = "Faculté d'Informatique", Code = "FI", University = university };
        var department = new Department { Name = "Département Informatique", Code = "INFO", Faculty = faculty };
        var domain = new AcademicDomain { Name = "Mathématiques et Informatique", Code = "MI", Faculty = faculty };
        var specialty = new Specialty { Name = "Informatique", Code = "INFO", Department = department, AcademicDomain = domain };

        // Wire the collection navigations so EF discovers the whole graph from the University root.
        university.Faculties.Add(faculty);
        faculty.Departments.Add(department);
        faculty.Domains.Add(domain);
        department.Specialties.Add(specialty);
        domain.Specialties.Add(specialty);

        var years = new[]
        {
            new AcademicYear { StartYear = 2023, EndYear = 2024, Name = "2023-2024" },
            new AcademicYear { StartYear = 2024, EndYear = 2025, Name = "2024-2025", IsCurrent = true },
        };
        var sessions = new[]
        {
            new Session { Name = "Session normale", Kind = SessionKind.Normal, Order = 1 },
            new Session { Name = "Session de rattrapage", Kind = SessionKind.Retake, Order = 2 },
        };

        var moduleNames = new (string Level, string Short, int Cycle, int LvlOrder, string[] S1, string[] S2)[]
        {
            ("Licence 1", "L1", 1, 1,
                new[] { "Algorithmique et structures de données 1", "Analyse 1", "Algèbre 1" },
                new[] { "Algorithmique et structures de données 2", "Analyse 2", "Structure machine 1" }),
            ("Licence 2", "L2", 1, 2,
                new[] { "Bases de données", "Programmation orientée objet", "Probabilités et statistiques" },
                new[] { "Systèmes d'information", "Théorie des langages", "Architecture des ordinateurs" }),
            ("Licence 3", "L3", 1, 3,
                new[] { "Systèmes d'exploitation", "Réseaux", "Génie logiciel" },
                new[] { "Compilation", "Sécurité informatique", "Projet de fin de cycle" }),
        };

        var semesterGlobalOrder = 1;
        foreach (var lvl in moduleNames)
        {
            var level = new Level
            {
                Name = lvl.Level, ShortName = lvl.Short, Cycle = (StudyCycle)lvl.Cycle,
                Order = lvl.LvlOrder, Specialty = specialty,
            };

            foreach (var (halfName, halfShort, moduleTitles) in new[]
                     {
                         ($"Semestre {semesterGlobalOrder}", $"S{semesterGlobalOrder}", lvl.S1),
                         ($"Semestre {semesterGlobalOrder + 1}", $"S{semesterGlobalOrder + 1}", lvl.S2),
                     })
            {
                var semester = new Semester
                {
                    Name = halfName, ShortName = halfShort,
                    Order = int.Parse(halfShort.AsSpan(1)), Level = level,
                };

                foreach (var title in moduleTitles)
                {
                    semester.Modules.Add(new Module
                    {
                        Name = title, Coefficient = 2m, Credits = 6,
                        Specialty = specialty, Semester = semester,
                    });
                }

                level.Semesters.Add(semester);
            }

            semesterGlobalOrder += 2;
            specialty.Levels.Add(level);
        }

        AssignSlugs(university, faculty, department, domain, specialty);
        foreach (var y in years) { y.Slug = Slugifier.Slugify(y.Name); }
        foreach (var s in sessions) { s.Slug = Slugifier.Slugify(s.Name); }
        foreach (var level in specialty.Levels)
        {
            level.Slug = Slugifier.Slugify($"{specialty.Name}-{level.ShortName}");
            foreach (var semester in level.Semesters)
            {
                semester.Slug = Slugifier.Slugify($"{specialty.Name}-{semester.ShortName}");
                foreach (var module in semester.Modules)
                {
                    module.Slug = Slugifier.Slugify(module.Name);
                }
            }
        }

        _db.Universities.Add(university);
        _db.AcademicYears.AddRange(years);
        _db.Sessions.AddRange(sessions);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded sample USTHB academic tree ({Modules} modules).",
            specialty.Levels.SelectMany(l => l.Semesters).SelectMany(s => s.Modules).Count());
    }

    private static void AssignSlugs(params Domain.Common.AcademicEntity[] entities)
    {
        foreach (var entity in entities)
        {
            entity.Slug = Slugifier.Slugify(entity.Name);
        }
    }
}
