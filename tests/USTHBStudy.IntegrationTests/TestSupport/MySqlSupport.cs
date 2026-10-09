namespace USTHBStudy.IntegrationTests.TestSupport;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>
/// Marks tests that need a real MySQL server (true row-level concurrency, real migrations). They run when
/// <c>USTHB_TEST_MYSQL</c> holds a server connection string without a database, e.g.
/// <c>Server=localhost;User=usthb_app;Password=…;</c>, and are reported as skipped otherwise.
/// </summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public const string EnvVar = "USTHB_TEST_MYSQL";

    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar)))
        {
            Skip = $"Set {EnvVar} to a MySQL server connection string to run MySQL-backed tests.";
        }
    }

    public static string ServerConnection =>
        Environment.GetEnvironmentVariable(EnvVar) ?? throw new InvalidOperationException(EnvVar + " is not set.");

    public static string ConnectionFor(string database) =>
        new MySqlConnectionStringBuilder(ServerConnection) { Database = database, AllowUserVariables = true }.ConnectionString;

    public static async Task ExecuteAsync(string sql)
    {
        await using var connection = new MySqlConnection(ServerConnection);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>The real API against a throw-away MySQL database created by the real migrations.</summary>
public sealed class MySqlApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _database = "usthb_t_" + Guid.NewGuid().ToString("N")[..12];

    public Dictionary<string, string> Settings { get; } = new();

    public Action<IServiceCollection>? ConfigureExtraServices { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", MySqlFactAttribute.ConnectionFor(_database));
        builder.UseSetting("Jwt:Secret", "integration-tests-only-secret-key-0123456789abcdef");
        builder.UseSetting("Seed:DemoUsers", "false");
        builder.UseSetting("RateLimiting:PermitPerMinute", "100000");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "100000");
        builder.UseSetting("Storage:LocalRootPath", Path.Combine(Path.GetTempPath(), "usthb-tests", Guid.NewGuid().ToString("N")));
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services => ConfigureExtraServices?.Invoke(services));
    }

    public async Task InitializeAsync()
    {
        await MySqlFactAttribute.ExecuteAsync($"CREATE DATABASE `{_database}` CHARACTER SET utf8mb4;");

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(); // the real migrations, not EnsureCreated

        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync(includeDemoUsers: true, includeSampleAcademicData: false);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        MySqlConnection.ClearAllPools();
        await MySqlFactAttribute.ExecuteAsync($"DROP DATABASE IF EXISTS `{_database}`;");
    }
}
