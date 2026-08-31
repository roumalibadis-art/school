namespace USTHBStudy.API.Extensions;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Infrastructure.Persistence;

public static class WebApplicationExtensions
{
    /// <summary>Minimal, framework-agnostic security headers (PRD §44).</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers.Remove("X-Powered-By");
            await next();
        });

    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });
    }

    /// <summary>
    /// Applies migrations and seeds reference data at startup (PRD §52). Production never
    /// auto-migrates (PRD §6) — it only ensures roles exist; the schema is deployed separately.
    /// Skipped entirely under the <c>Testing</c> environment (the test host manages its own schema).
    /// </summary>
    public static async Task ApplyStartupAsync(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        var seeder = services.GetRequiredService<DbSeeder>();

        if (app.Environment.IsProduction())
        {
            logger.LogInformation("Production startup: ensuring roles/permissions (no auto-migration).");
            await seeder.SeedAsync(seedDemoUsers: false);
            return;
        }

        var db = services.GetRequiredService<AppDbContext>();
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();

        var seedDemoUsers = app.Configuration.GetValue("Seed:DemoUsers", app.Environment.IsDevelopment());
        await seeder.SeedAsync(seedDemoUsers);
        logger.LogInformation("Database is up to date and seeded (demo users: {SeedDemoUsers}).", seedDemoUsers);
    }
}
