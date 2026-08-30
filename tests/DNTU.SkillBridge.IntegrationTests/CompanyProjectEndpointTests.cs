using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class CompanyProjectEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";

    [Fact]
    public async Task Post_CreatesDraft_WithSkillsAndDeliverables_Persisted()
    {
        var client = await CreateCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(2);

        var created = await client.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title = "Hệ thống chấm điểm tự động",
            summary = "Bảng điều khiển chấm điểm",
            problemStatement = "Giảng viên mất thời gian chấm bài thủ công.",
            businessRequirements = "Hỗ trợ chấm hàng nghìn bài mỗi tuần.",
            technicalConstraints = "Triển khai trên nền tảng .NET.",
            difficulty = "INTERMEDIATE",
            workType = "HYBRID",
            durationWeeks = 8,
            expectedStudentCount = 4,
            minTeamSize = 3,
            maxTeamSize = 5,
            allowanceAmount = 5_000_000,
            allowanceCurrency = "VND",
            skills = new[]
            {
                new { skillId = skillIds[0], requirementLevel = "MUST_HAVE", isRequired = true },
                new { skillId = skillIds[1], requirementLevel = "NICE_TO_HAVE", isRequired = false }
            },
            deliverables = new[]
            {
                new { name = "Báo cáo phân tích", description = "Tài liệu phân tích yêu cầu", sortOrder = 1 },
                new { name = "Bản demo", description = string.Empty, sortOrder = 2 }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var payload = (await created.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data;
        Assert.Equal("DRAFT", payload.Status);
        Assert.NotEmpty(payload.Slug);
        Assert.StartsWith("PRJ-", payload.Code);
        Assert.Equal(2, payload.Skills.Count);
        Assert.Contains(payload.Skills, skill => skill.RequirementLevel == "MUST_HAVE" && skill.IsRequired);
        Assert.Equal(2, payload.Deliverables.Count);

        var fetched = (await client.GetFromJsonAsync<Envelope<ProjectDetailDto>>(
            $"/api/v1/company/projects/{payload.Id}", Json))!.Data;
        Assert.Equal(payload.Slug, fetched.Slug);
        Assert.Equal(2, fetched.Skills.Count);
        Assert.Equal(2, fetched.Deliverables.Count);

        var progress = (await client.GetFromJsonAsync<Envelope<ProgressDto>>(
            $"/api/v1/company/projects/{payload.Id}/progress", Json))!.Data;
        Assert.True(progress.IsDraft);
        Assert.Equal(2, progress.DeliverableCount);
        Assert.Equal(2, progress.SkillCount);

        var team = (await client.GetFromJsonAsync<Envelope<List<object>>>(
            $"/api/v1/company/projects/{payload.Id}/team", Json))!.Data;
        Assert.Empty(team);
    }

    [Fact]
    public async Task Projects_AreCompanyScoped_OtherCompanyGets404()
    {
        var owner = await CreateCompanyAsync();
        var outsider = await CreateCompanyAsync();
        var projectId = await CreateProjectAsync(owner, "Dự án riêng tư A");

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/company/projects/{projectId}")).StatusCode);

        var foreignPut = await outsider.PutAsJsonAsync($"/api/v1/company/projects/{projectId}", DraftPayload("Chiếm quyền"));
        Assert.Equal(HttpStatusCode.NotFound, foreignPut.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.DeleteAsync($"/api/v1/company/projects/{projectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/company/projects/{projectId}/progress")).StatusCode);
    }

    [Fact]
    public async Task Put_Draft_UpdatesFields_AndReplacesSkills()
    {
        var client = await CreateCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(3);
        var projectId = await CreateProjectAsync(client, "Tiêu đề cũ", skillIds[0..2]);

        var updated = await client.PutAsJsonAsync($"/api/v1/company/projects/{projectId}", new
        {
            title = "Tiêu đề mới hoàn toàn",
            summary = "Tóm tắt mới",
            difficulty = "ADVANCED",
            workType = "REMOTE",
            durationWeeks = 12,
            expectedStudentCount = 5,
            minTeamSize = 2,
            maxTeamSize = 6,
            allowanceAmount = 9_000_000,
            allowanceCurrency = "VND",
            skills = new[]
            {
                new { skillId = skillIds[2], requirementLevel = "IMPORTANT", isRequired = true }
            },
            deliverables = new[]
            {
                new { name = "Sản phẩm bàn giao", description = "Mô tả bàn giao", sortOrder = 1 }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var payload = (await updated.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data;
        Assert.Equal("Tiêu đề mới hoàn toàn", payload.Title);
        Assert.Equal("ADVANCED", payload.Difficulty);
        Assert.Equal("REMOTE", payload.WorkType);
        Assert.Single(payload.Skills);
        Assert.Equal(skillIds[2], payload.Skills.First().SkillId);
        Assert.Single(payload.Deliverables);
    }

    [Fact]
    public async Task Post_RejectsInvalidCatalog_AndInvalidTeamSize()
    {
        var client = await CreateCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);

        var badIndustry = await client.PostAsJsonAsync("/api/v1/company/projects", DraftPayload(
            "Dự án ngành sai", industryId: Guid.NewGuid(), skillIds: [skillIds[0]]), Json);
        Assert.Equal(HttpStatusCode.BadRequest, badIndustry.StatusCode);

        var unknownSkill = await client.PostAsJsonAsync("/api/v1/company/projects", DraftPayload(
            "Dự án kỹ năng lạ", skillIds: [Guid.NewGuid()]), Json);
        Assert.Equal(HttpStatusCode.BadRequest, unknownSkill.StatusCode);

        var badTeamSize = await client.PostAsJsonAsync("/api/v1/company/projects", DraftPayload(
            "Dự án đội hình sai", skillIds: [skillIds[0]], minTeamSize: 4, maxTeamSize: 2), Json);
        Assert.Equal(HttpStatusCode.BadRequest, badTeamSize.StatusCode);
    }

    [Fact]
    public async Task List_IsPaged_AndSupportsTitleSort()
    {
        var client = await CreateCompanyAsync();
        await CreateProjectAsync(client, "Dự án B");
        await CreateProjectAsync(client, "Dự án A");
        await CreateProjectAsync(client, "Dự án C");

        var titlePage = (await client.GetFromJsonAsync<ProjectListDto>(
            "/api/v1/company/projects?page=1&pageSize=2&sort=title", Json))!;
        Assert.Equal(3, titlePage.Meta.TotalItems);
        Assert.Equal(2, titlePage.Meta.TotalPages);
        Assert.Equal(["Dự án A", "Dự án B"], titlePage.Data.Select(project => project.Title).ToArray());

        var newest = (await client.GetFromJsonAsync<ProjectListDto>(
            "/api/v1/company/projects?page=2&pageSize=2&sort=unknown-sort", Json))!;
        Assert.Single(newest.Data);
        Assert.Equal("Dự án B", newest.Data.First().Title); // default newest order: C, A, B (creation order B, A, C)
    }

    [Fact]
    public async Task Delete_Draft_SoftDeletes_AndGetReturns404()
    {
        var client = await CreateCompanyAsync();
        var projectId = await CreateProjectAsync(client, "Dự án bị xóa");

        var deleted = await client.DeleteAsync($"/api/v1/company/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/company/projects/{projectId}")).StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.False(await dbContext.Projects
                .Where(project => project.Id == projectId)
                .Select(project => project.IsActive)
                .SingleAsync());
        });
    }

    [Fact]
    public async Task SameTitle_AcrossCompanies_GetsUniqueSlugs()
    {
        var first = await CreateCompanyAsync();
        var second = await CreateCompanyAsync();
        var title = $"Cùng một tiêu đề dự án {Guid.NewGuid():N}";

        var firstId = await CreateProjectAsync(first, title);
        var secondId = await CreateProjectAsync(second, title);

        var firstProject = (await first.GetFromJsonAsync<Envelope<ProjectDetailDto>>(
            $"/api/v1/company/projects/{firstId}", Json))!.Data;
        var secondProject = (await second.GetFromJsonAsync<Envelope<ProjectDetailDto>>(
            $"/api/v1/company/projects/{secondId}", Json))!.Data;
        Assert.NotEqual(firstProject.Slug, secondProject.Slug);
        Assert.StartsWith(firstProject.Slug, secondProject.Slug);
    }

    private async Task<HttpClient> CreateCompanyAsync()
    {
        var client = await RegisterCompanyAsync();
        var created = await client.PutAsJsonAsync("/api/v1/companies/me",
            new { name = $"Công ty Dự án {Guid.NewGuid():N}" }, Json);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return client;
    }

    private async Task<HttpClient> RegisterCompanyAsync()
    {
        var email = $"company-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Kiểm thử",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<List<Guid>> GetActiveSkillIdsAsync(int count)
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<ListEnvelope<CatalogSkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        return skills.Take(count).Select(skill => skill.Id).ToList();
    }

    private static object DraftPayload(
        string title,
        Guid? industryId = null,
        IReadOnlyCollection<Guid>? skillIds = null,
        int minTeamSize = 3,
        int maxTeamSize = 5,
        int expectedStudentCount = 4) => new
        {
            title,
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            industryId,
            expectedStudentCount,
            minTeamSize,
            maxTeamSize,
            skills = (skillIds ?? []).Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
            deliverables = new[] { new { name = "Bàn giao mặc định", description = (string?)null, sortOrder = 1 } }
        };

    private async Task<Guid> CreateProjectAsync(HttpClient client, string title, IReadOnlyCollection<Guid>? skillIds = null)
    {
        var created = await client.PostAsJsonAsync("/api/v1/company/projects", DraftPayload(title, skillIds: skillIds), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data.Id;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);

    private sealed record AuthDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId);

    private sealed record CatalogSkillDto(Guid Id, string Code, string Name, string Category, string? Description, bool IsActive);

    private sealed record ProjectSkillDto(Guid SkillId, string SkillCode, string SkillName, string RequirementLevel, bool IsRequired);

    private sealed record ProjectDeliverableDto(Guid Id, string Name, string? Description, int SortOrder);

    private sealed record ProjectDetailDto(
        Guid Id, Guid CompanyId, string Code, string Title, string Slug, string Status, bool IsActive,
        string? Summary, string? ProblemStatement, string? BusinessRequirements, string? TechnicalConstraints,
        Guid? IndustryId, string? IndustryName, string Difficulty, string WorkType, int DurationWeeks,
        DateTimeOffset? ApplicationDeadline, int ExpectedStudentCount, int MinTeamSize, int MaxTeamSize,
        long? AllowanceAmount, string? AllowanceCurrency,
        IReadOnlyCollection<ProjectSkillDto> Skills, IReadOnlyCollection<ProjectDeliverableDto> Deliverables,
        DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    private sealed record ProjectListDto(IReadOnlyCollection<ProjectListItemDto> Data, MetaDto Meta);

    private sealed record ProjectListItemDto(Guid Id, string Code, string Title, string Slug, string Status);

    private sealed record MetaDto(int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record ProgressDto(Guid ProjectId, string Status, int DeliverableCount, int SkillCount, bool IsDraft);
}
