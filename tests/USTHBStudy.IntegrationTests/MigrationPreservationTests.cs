namespace USTHBStudy.IntegrationTests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Infrastructure.Persistence;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>
/// Spec test 16: the CommunityClassification migration keeps every existing row, classifies legacy documents so
/// they stay out of the community queue, and rolls back without losing data (or refuses to).
/// </summary>
public class MigrationPreservationTests
{
    private const string PreviousMigration = "20260831135931_AdminFeatures";

    private static async Task<AppDbContext> CreateDatabaseAsync(string name)
    {
        await MySqlFactAttribute.ExecuteAsync($"DROP DATABASE IF EXISTS `{name}`; CREATE DATABASE `{name}` CHARACTER SET utf8mb4;");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(MySqlFactAttribute.ConnectionFor(name), new MySqlServerVersion(new Version(8, 0, 36)),
                m => m.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task DropAsync(AppDbContext db, string name)
    {
        await db.DisposeAsync();
        MySqlConnection.ClearAllPools();
        await MySqlFactAttribute.ExecuteAsync($"DROP DATABASE IF EXISTS `{name}`;");
    }

    private static async Task RunSqlFileAsync(string database, string path)
    {
        await using var connection = new MySqlConnection(MySqlFactAttribute.ConnectionFor(database));
        await connection.OpenAsync();
        await using var command = new MySqlCommand(await File.ReadAllTextAsync(path), connection);
        await command.ExecuteNonQueryAsync();
    }

    [MySqlFact]
    public async Task Upgrading_a_populated_database_keeps_all_data_and_keeps_legacy_documents_out_of_the_queue()
    {
        var name = "usthb_mig_" + Guid.NewGuid().ToString("N")[..10];
        var db = await CreateDatabaseAsync(name);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await RunSqlFileAsync(name, Path.Combine(AppContext.BaseDirectory, "MigrationData", "legacy-seed.sql"));

            await migrator.MigrateAsync(); // → CommunityClassification

            var documents = await db.Documents.AsNoTracking().OrderBy(d => d.Slug).ToListAsync();
            documents.Should().HaveCount(2);
            foreach (var d in documents)
            {
                d.ModuleId.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000007"));
                d.ClassificationStatus.Should().Be(ClassificationStatus.Classified, "legacy documents already carry staff metadata");
                d.VerificationStatus.Should().Be(VerificationStatus.Unverified);
                d.VotingRound.Should().Be(1);
                d.ClassificationVersion.Should().Be(0);
                d.ClassifiedAt.Should().NotBeNull();
            }

            var published = documents.Single(d => d.Slug == "legacy-published-exam");
            (published.ViewCount, published.DownloadCount, published.FileSize).Should().Be((12, 3, 100));
            documents.Single(d => d.Slug == "legacy-draft-course").IsPremium.Should().BeTrue();

            (await db.Universities.CountAsync()).Should().Be(1, "the university entity and its data are untouched");
            (await db.Faculties.CountAsync()).Should().Be(1);
            (await db.Modules.CountAsync()).Should().Be(1);

            // New tables exist and are empty; nothing legacy leaks into the community queue.
            (await db.ClassificationVotes.CountAsync()).Should().Be(0);
            (await db.Documents.CountAsync(d => d.ClassificationStatus == ClassificationStatus.Unclassified)).Should().Be(0);
        }
        finally
        {
            await DropAsync(db, name);
        }
    }

    [MySqlFact]
    public async Task Rolling_back_restores_the_previous_schema_with_data_intact_and_reapplying_works()
    {
        var name = "usthb_mig_" + Guid.NewGuid().ToString("N")[..10];
        var db = await CreateDatabaseAsync(name);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await RunSqlFileAsync(name, Path.Combine(AppContext.BaseDirectory, "MigrationData", "legacy-seed.sql"));
            await migrator.MigrateAsync();

            await migrator.MigrateAsync(PreviousMigration); // roll back

            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            applied.Should().EndWith(PreviousMigration);
            await using (var connection = new MySqlConnection(MySqlFactAttribute.ConnectionFor(name)))
            {
                await connection.OpenAsync();
                await using var cmd = new MySqlCommand("SELECT COUNT(*), SUM(ViewCount) FROM Documents", connection);
                await using var reader = await cmd.ExecuteReaderAsync();
                (await reader.ReadAsync()).Should().BeTrue();
                reader.GetInt64(0).Should().Be(2);
                reader.GetDecimal(1).Should().Be(12);
            }

            await migrator.MigrateAsync(); // and forward again
            (await db.Documents.CountAsync(d => d.ClassificationStatus == ClassificationStatus.Classified)).Should().Be(2);
        }
        finally
        {
            await DropAsync(db, name);
        }
    }

    [MySqlFact]
    public async Task Rollback_refuses_rather_than_destroying_documents_that_have_no_module()
    {
        var name = "usthb_mig_" + Guid.NewGuid().ToString("N")[..10];
        var db = await CreateDatabaseAsync(name);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await RunSqlFileAsync(name, Path.Combine(AppContext.BaseDirectory, "MigrationData", "legacy-seed.sql"));
            await migrator.MigrateAsync();
            await MySqlFactAttribute.ExecuteAsync($@"
                INSERT INTO `{name}`.Documents (Id, Title, Slug, Type, Status, ModuleId, FileStorageKey, FileName, FileSize, MimeType,
                                                FileHashSha256, IsPremium, RightsStatus, ViewCount, DownloadCount, IsDeleted, CreatedAt, UpdatedAt,
                                                ClassificationStatus, VerificationStatus, VotingRound, ClassificationVersion)
                VALUES ('00000000-0000-0000-0000-0000000000d9', 'Unclassified', 'unclassified', 99, 1, NULL, 'k', 'f.pdf', 1, 'application/pdf',
                        'CC', 0, 0, 0, 0, 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 0, 1, 1, 0);");

            var rollback = () => migrator.MigrateAsync(PreviousMigration);

            await rollback.Should().ThrowAsync<Exception>("restoring NOT NULL on ModuleId must not silently drop or invent data");
            MySqlConnection.ClearAllPools();
            (await db.Documents.IgnoreQueryFilters().CountAsync()).Should().Be(3, "no rows were lost by the failed rollback");
        }
        finally
        {
            await DropAsync(db, name);
        }
    }
}
