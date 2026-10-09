namespace USTHBStudy.IntegrationTests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using USTHBStudy.Infrastructure.Persistence;
using USTHBStudy.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Hosts the real API pipeline for integration tests, with the MySQL context swapped for a
/// private SQLite in-memory database (schema via <c>EnsureCreated</c>, roles + demo users seeded).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Extra configuration / service overrides for a specific test (applied when the host is built).</summary>
    public Dictionary<string, string> Settings { get; } = new();

    public Action<IServiceCollection>? ConfigureExtraServices { get; set; }

    public const string AdminEmail = "admin@example.local";
    public const string AdminPassword = "Admin#2026!";
    public const string StudentEmail = "student@example.local";
    public const string StudentPassword = "Student#2026!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Default", "Server=localhost;Database=placeholder;User=placeholder;Password=placeholder");
        builder.UseSetting("Jwt:Secret", "integration-tests-only-secret-key-0123456789abcdef");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "14");
        builder.UseSetting("Seed:DemoUsers", "false");
        builder.UseSetting("RateLimiting:PermitPerMinute", "100000");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "100000");
        builder.UseSetting(
            "Storage:LocalRootPath",
            Path.Combine(Path.GetTempPath(), "usthb-tests", Guid.NewGuid().ToString("N")));

        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlite(_connection);
                options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
            });

            ConfigureExtraServices?.Invoke(services);
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync(includeDemoUsers: true, includeSampleAcademicData: false);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }
}
