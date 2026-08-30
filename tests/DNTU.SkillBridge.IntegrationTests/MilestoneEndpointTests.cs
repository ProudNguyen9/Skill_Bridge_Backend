using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class MilestoneEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";

    [Fact]
    public async Task Company_can_manage_milestone_and_stale_transition_is_rejected()
    {
        var company = await CreateCompanyAsync();
        var projectId = await CreateProjectAsync(company);
        var created = await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/milestones", new
        {
            title = "Thiết kế kiến trúc",
            sequence = 1,
            dueAt = DateTimeOffset.UtcNow.AddDays(14)
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var milestone = (await created.Content.ReadFromJsonAsync<Envelope<MilestoneDto>>(Json))!.Data;

        var started = await company.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/start", new { version = milestone.Version }, Json);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var inProgress = (await started.Content.ReadFromJsonAsync<Envelope<MilestoneDto>>(Json))!.Data;
        Assert.Equal("IN_PROGRESS", inProgress.Status);

        var stale = await company.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/start", new { version = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var listed = await company.GetFromJsonAsync<Envelope<IReadOnlyCollection<MilestoneDto>>>($"/api/v1/projects/{projectId}/milestones", Json);
        Assert.Single(listed!.Data);
        Assert.Equal("IN_PROGRESS", listed.Data.Single().Status);
    }

    [Fact]
    public async Task Foreign_company_cannot_view_or_attach_file_to_milestone()
    {
        var owner = await CreateCompanyAsync();
        var outsider = await CreateCompanyAsync();
        var projectId = await CreateProjectAsync(owner);
        var created = await owner.PostAsJsonAsync($"/api/v1/projects/{projectId}/milestones", new
        {
            title = "Kiểm thử nghiệm thu",
            sequence = 1,
            dueAt = DateTimeOffset.UtcNow.AddDays(14)
        }, Json);
        var milestone = (await created.Content.ReadFromJsonAsync<Envelope<MilestoneDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/milestones/{milestone.Id}")).StatusCode);
        var attach = await outsider.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/deliverables", new
        {
            name = "Không có quyền",
            criteria = "Tệp phải thuộc workspace",
            fileId = Guid.NewGuid()
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, attach.StatusCode);
    }

    private async Task<HttpClient> CreateCompanyAsync()
    {
        var email = $"milestone-company-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "Công ty milestone", password = Password, accountType = "Company" }, Json)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/companies/me", new { name = $"Công ty milestone {Guid.NewGuid():N}" }, Json)).StatusCode);
        return client;
    }

    private async Task<Guid> CreateProjectAsync(HttpClient company)
    {
        var response = await company.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title = $"Dự án milestone {Guid.NewGuid():N}",
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            expectedStudentCount = 3,
            minTeamSize = 2,
            maxTeamSize = 4,
            skills = Array.Empty<object>(),
            deliverables = new[] { new { name = "Bàn giao", description = (string?)null, sortOrder = 1 } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<ProjectDto>>(Json))!.Data.Id;
    }

    private sealed record Envelope<T>(T Data);
    private sealed record AuthDto(string AccessToken);
    private sealed record ProjectDto(Guid Id);
    private sealed record MilestoneDto(Guid Id, string Status, Guid Version, bool IsOverdue);
}
