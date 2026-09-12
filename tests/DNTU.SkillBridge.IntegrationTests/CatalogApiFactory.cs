using System.Text.Json;
using System.Text.Json.Serialization;
using DNTU.SkillBridge.Infrastructure.Persistence;
using DNTU.SkillBridge.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace DNTU.SkillBridge.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class CatalogApiCollection : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "catalog-api";
}

/// <summary>
/// Boots the real API against a dedicated local SQL Server database (skillbridge_tests).
/// The fixture uses SQL Server directly, recreates its isolated database, applies
/// every migration, and lets Program.cs run identity + catalog seeding.
/// Set SKILLBRIDGE_TESTS_SQLSERVER to override the master connection string.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultMasterConnectionString =
        "Server=(localdb)\\SkillBridge2022;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
    private static readonly string DatabaseName =
        $"skillbridge_tests_{Environment.ProcessId}_{Guid.NewGuid().ToString("N")[..8]}";

    private readonly string _masterConnectionString =
        Environment.GetEnvironmentVariable("SKILLBRIDGE_TESTS_SQLSERVER") ?? DefaultMasterConnectionString;

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
        var builder = new SqlConnectionStringBuilder(_masterConnectionString) { InitialCatalog = "master" };
        await using (var masterConnection = new SqlConnection(builder.ConnectionString))
        {
            await masterConnection.OpenAsync();

            await using var drop = new SqlCommand($"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END;", masterConnection);
            await drop.ExecuteNonQueryAsync();

            await using var create = new SqlCommand(
                $"CREATE DATABASE [{DatabaseName}] COLLATE Latin1_General_100_CI_AS_SC;",
                masterConnection);
            await create.ExecuteNonQueryAsync();
        }

        _connectionString = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = DatabaseName
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

        var builder = new SqlConnectionStringBuilder(_masterConnectionString) { InitialCatalog = "master" };
        await using var masterConnection = new SqlConnection(builder.ConnectionString);
        await masterConnection.OpenAsync();
        await using var drop = new SqlCommand($"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END;", masterConnection);
        await drop.ExecuteNonQueryAsync();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_connectionString)
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
