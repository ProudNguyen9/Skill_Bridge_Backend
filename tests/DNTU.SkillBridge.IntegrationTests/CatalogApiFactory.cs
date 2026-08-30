using System.Text.Json;
using System.Text.Json.Serialization;
using DNTU.SkillBridge.Infrastructure.Persistence;
using DNTU.SkillBridge.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DNTU.SkillBridge.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class CatalogApiCollection : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "catalog-api";
}

/// <summary>
/// Boots the real API against a dedicated local PostgreSQL database (skillbridge_tests).
/// Docker/Testcontainers is not available on this machine, so the fixture recreates the
/// database, applies every migration, and lets Program.cs run identity + catalog seeding.
/// Set SKILLBRIDGE_TESTS_PG to override the master connection string.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultMasterConnectionString =
        "Host=localhost;Port=5432;Username=postgres;Password=123456;Database=postgres";
    private static readonly string DatabaseName =
        $"skillbridge_tests_{Environment.ProcessId}_{Guid.NewGuid().ToString("N")[..8]}";

    private readonly string _masterConnectionString =
        Environment.GetEnvironmentVariable("SKILLBRIDGE_TESTS_PG") ?? DefaultMasterConnectionString;

    private string _connectionString = string.Empty;

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        // UseSetting feeds the host configuration, which takes precedence over appsettings.json
        // in the minimal hosting model used by Program.cs.
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-with-at-least-sixty-four-characters-0001");
    }

    async Task IAsyncLifetime.InitializeAsync() => await InitializeAsyncCoreAsync();

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsyncCoreAsync();

    private async Task InitializeAsyncCoreAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_masterConnectionString) { Database = "postgres" };
        await using (var masterConnection = new NpgsqlConnection(builder.ConnectionString))
        {
            await masterConnection.OpenAsync();

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE);", masterConnection);
            await drop.ExecuteNonQueryAsync();

            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE {DatabaseName} ENCODING 'UTF8' TEMPLATE template0 LC_COLLATE 'C' LC_CTYPE 'C';",
                masterConnection);
            await create.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(_masterConnectionString)
        {
            Database = DatabaseName
        }.ConnectionString;

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        // Force one app start so Program.cs identity + catalog seeding happens before any test runs.
        _ = CreateClient();
    }

    private async Task DisposeAsyncCoreAsync()
    {
        if (Environment.GetEnvironmentVariable("SKILLBRIDGE_TESTS_KEEP_DB") == "1")
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(_masterConnectionString) { Database = "postgres" };
        await using var masterConnection = new NpgsqlConnection(builder.ConnectionString);
        await masterConnection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE);", masterConnection);
        await drop.ExecuteNonQueryAsync();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options);

    public async Task RunWithDbContextAsync(Func<AppDbContext, Task> action)
    {
        await using var dbContext = CreateDbContext();
        await action(dbContext);
    }

    public async Task ReSeedCatalogAsync()
    {
        await using var dbContext = CreateDbContext();
        await CatalogSeed.SeedAsync(dbContext, CancellationToken.None);
    }
}

public static class CatalogApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
