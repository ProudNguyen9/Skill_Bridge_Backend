using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace DNTU.SkillBridge.FunctionalTests;

/// <summary>
/// Boots the API against the developer's configured local SQL Server database to
/// verify public HTTP contracts end-to-end. It does not mutate application data.
/// </summary>
public sealed class ApiContractSmokeTests : IAsyncLifetime
{
    private static readonly string ConnectionString = Environment.GetEnvironmentVariable("SKILLBRIDGE_FUNCTIONAL_SQLSERVER")
        ?? "Server=(localdb)\\SkillBridge2022;Database=skillbridge;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;

    public async Task InitializeAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.UseSetting("ConnectionStrings:Default", ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "functional-tests-signing-key-with-at-least-sixty-four-characters-0001");
            });
        client = factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task PublicHealthAndOpenApiEndpoints_AreAvailable()
    {
        Assert.NotNull(client);
        var live = await client.GetAsync("/health/live");
        var openApi = await client.GetAsync("/openapi/v1.json");
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    [Fact]
    public async Task OpenApi_ExposesVersionedFileLifecycleRoutes()
    {
        Assert.NotNull(client);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/v1/files/upload-requests", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/files/{fileId}/complete", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/files/{fileId}/download-url", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_ExposesTechnicalReviewAndHistoryContracts()
    {
        Assert.NotNull(client);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/v1/lecturer/submissions/{submissionId}/technical-review", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/lecturer/reviews", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/lecturer/reviews/{reviewId}", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/submissions/{submissionId}/reviews", document, StringComparison.Ordinal);
        Assert.Contains("criteriaNotes", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_ExposesGovernedCommitmentAndWithdrawalWorkflowContracts()
    {
        Assert.NotNull(client);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/v1/projects/{projectId}/commitment", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/projects/{projectId}/commitment/confirm", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/students/me/commitments", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/withdrawals", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/withdrawals/{withdrawalId}/recommend", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/withdrawals/{withdrawalId}/approve", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/withdrawals/{withdrawalId}/reject", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_ExposesWorkspaceMembershipOverviewActivityAndSettingsContracts()
    {
        Assert.NotNull(client);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/v1/workspaces/{projectId}/overview", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/workspaces/{projectId}/team", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/workspaces/{projectId}/activity", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/workspaces/{projectId}/settings", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/projects/{projectId}/activity", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/students/me/projects", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/students/me/projects/{projectId}", document, StringComparison.Ordinal);
        Assert.Contains("membersCanCreateTasks", document, StringComparison.Ordinal);
        Assert.Contains("eventType", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_ExposesKanbanTaskLifecycleAndConcurrencyContracts()
    {
        Assert.NotNull(client);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/v1/projects/{projectId}/tasks", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/projects/{projectId}/board", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/tasks/{taskId}", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/tasks/{taskId}/move", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/tasks/{taskId}/assign", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/tasks/{taskId}/unassign", document, StringComparison.Ordinal);
        Assert.Contains("version", document, StringComparison.Ordinal);
        Assert.Contains("assigneeStudentId", document, StringComparison.Ordinal);
        Assert.Contains("sortOrder", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_ExposesCompanyBusinessReviewContractsWithoutAcademicScoringFields()
    {
        Assert.NotNull(client);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/v1/company/submissions/{submissionId}/business-review", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/company/business-reviews", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/submissions/{submissionId}/business-reviews", document, StringComparison.Ordinal);
        Assert.Contains("requirementsFeedback", document, StringComparison.Ordinal);
        Assert.Contains("collaborationFeedback", document, StringComparison.Ordinal);
        Assert.DoesNotContain("totalAcademicScore", document, StringComparison.OrdinalIgnoreCase);
    }
}
