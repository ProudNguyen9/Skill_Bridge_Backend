using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// Task 15: students save publicly visible projects (idempotently), see them in a paged
/// list, and get a deterministic skill match against active declared skills. Drafts and
/// hidden projects answer 404; non-students are forbidden.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class SavedProjectEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";

    /// <summary>Unique ASCII prefix baked into every seeded title of this test instance.</summary>
    private readonly string prefix = $"zs{Guid.NewGuid():N}"[..10];
    private string SavedTitle => $"{prefix} Saved Platform";
    private string DraftTitle => $"{prefix} Draft Hidden";

    [Fact]
    public async Task Save_VisibleProject_ReturnsList_And_IsIdempotent()
    {
        var student = await RegisterStudentAsync();
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(2);
        var projectId = await PublishProjectAsync(company, admin, SavedTitle, skillIds);

        var save = await student.PostAsync($"/api/v1/students/me/saved-projects/{projectId}", null);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var list = (await save.Content.ReadFromJsonAsync<PagedListDto>(Json))!;
        Assert.Equal(1, list.Meta.TotalItems);
        Assert.Contains(list.Data, item => item.ProjectId == projectId && item.Title == SavedTitle);
        Assert.False(string.IsNullOrWhiteSpace(list.Data.Single(item => item.ProjectId == projectId).CompanyName));

        // Second save is idempotent: still a single row.
        var saveAgain = await student.PostAsync($"/api/v1/students/me/saved-projects/{projectId}", null);
        Assert.Equal(HttpStatusCode.OK, saveAgain.StatusCode);
        var listAgain = (await saveAgain.Content.ReadFromJsonAsync<PagedListDto>(Json))!;
        Assert.Equal(1, listAgain.Meta.TotalItems);

        // Exactly one row in the database for the pair; the project id is unique to this test.
        var rowCount = 0;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            rowCount = await dbContext.SavedProjects
                .CountAsync(saved => saved.ProjectId == projectId);
        });
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task Save_DraftProject_Returns404()
    {
        var student = await RegisterStudentAsync();
        var company = await CreateVerifiedCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);
        var draftId = await CreateProjectAsync(company, DraftTitle, skillIds);

        var response = await student.PostAsync($"/api/v1/students/me/saved-projects/{draftId}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unsave_RemovesBookmark_FromList()
    {
        var student = await RegisterStudentAsync();
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);
        var projectId = await PublishProjectAsync(company, admin, SavedTitle, skillIds);

        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsync($"/api/v1/students/me/saved-projects/{projectId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await student.DeleteAsync($"/api/v1/students/me/saved-projects/{projectId}")).StatusCode);

        var list = (await (await student.GetAsync("/api/v1/students/me/saved-projects"))
            .Content.ReadFromJsonAsync<PagedListDto>(Json))!;
        Assert.Equal(0, list.Meta.TotalItems);

        // Unsaving again answers 404; the bookmark is gone.
        Assert.Equal(HttpStatusCode.NotFound,
            (await student.DeleteAsync($"/api/v1/students/me/saved-projects/{projectId}")).StatusCode);
    }

    [Fact]
    public async Task SkillMatch_ComparesDeclaredSkills_WithRequirements()
    {
        var student = await RegisterStudentAsync();
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(2);

        // Student declares only the first of the two required skills.
        var replace = await student.PutAsJsonAsync("/api/v1/students/me/skills", new
        {
            skills = new object[] { new { skillId = skillIds[0], level = "ADVANCED" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);

        var projectId = await PublishProjectAsync(company, admin, SavedTitle, skillIds);

        var response = await student.GetAsync($"/api/v1/students/me/skill-match/{projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var match = (await response.Content.ReadFromJsonAsync<Envelope<SkillMatchDto>>(Json))!.Data;

        Assert.Equal(projectId, match.ProjectId);
        Assert.Equal(2, match.RequiredCount);
        Assert.Equal(1, match.MatchedCount);
        Assert.Equal(50, match.MatchPercent);
        Assert.Contains(match.Matched, item => item.SkillId == skillIds[0]);
        Assert.Contains(match.Missing, item => item.SkillId == skillIds[1]);
        Assert.DoesNotContain(match.Missing, item => item.SkillId == skillIds[0]);
    }

    [Fact]
    public async Task SkillMatch_OnInvisibleProject_Returns404()
    {
        var student = await RegisterStudentAsync();
        var company = await CreateVerifiedCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);
        var draftId = await CreateProjectAsync(company, DraftTitle, skillIds);

        var response = await student.GetAsync($"/api/v1/students/me/skill-match/{draftId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_Student_Role_IsForbidden()
    {
        var email = $"company-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty TNHH Lưu dự án",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/students/me/saved-projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/students/me/saved-projects/{Guid.NewGuid()}", null)).StatusCode);
    }

    private async Task<HttpClient> RegisterStudentAsync()
    {
        var email = $"student-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Sinh viên Lưu dự án",
            password = Password,
            accountType = "Student"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateVerifiedCompanyAsync()
    {
        var client = await RegisterCompanyAsync();
        var companyName = $"Công ty Lưu dự án {Guid.NewGuid():N}";
        var created = await client.PutAsJsonAsync("/api/v1/companies/me", new { name = companyName }, Json);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var company = await dbContext.Companies.SingleAsync(item => item.Name == companyName);
            company.MarkVerified(DateTimeOffset.UtcNow, null);
            await dbContext.SaveChangesAsync();
        });
        return client;
    }

    private async Task<HttpClient> RegisterCompanyAsync()
    {
        var email = $"saved-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Lưu dự án",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        Guid adminUserId = default;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new User($"admin-{Guid.NewGuid():N}@skillbridge.local", "Quản trị viên", "seed-hash");
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            adminUserId = user.Id;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(RoleNames.Admin, adminUserId));
        return client;
    }

    private static string CreateToken(string role, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            "DNTU.SkillBridge.Api",
            "DNTU.SkillBridge.Web",
            claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<List<Guid>> GetActiveSkillIdsAsync(int count)
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<ListEnvelope<CatalogSkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        Assert.True(skills.Count >= count, $"Need at least {count} active skills for saved-project tests.");
        return skills.Take(count).Select(skill => skill.Id).ToList();
    }

    private async Task<Guid> PublishProjectAsync(HttpClient company, HttpClient admin, string title, IReadOnlyList<Guid> skillIds)
    {
        var projectId = await CreateProjectAsync(company, title, skillIds);
        var updated = await company.PutAsJsonAsync($"/api/v1/company/projects/{projectId}",
            CompletePayload(title, skillIds), Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);
        return projectId;
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client, string title, IReadOnlyList<Guid> skillIds)
    {
        var created = await client.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title,
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            expectedStudentCount = 3,
            minTeamSize = 2,
            maxTeamSize = 4,
            skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data.Id;
    }

    private static object CompletePayload(string title, IReadOnlyList<Guid> skillIds) => new
    {
        title,
        summary = $"Tóm tắt lưu dự án cho {title}",
        problemStatement = "Vấn đề thực tế cần giải quyết.",
        businessRequirements = "Yêu cầu nghiệp vụ rõ ràng.",
        technicalConstraints = "Ràng buộc kỹ thuật .NET.",
        difficulty = "BEGINNER",
        workType = "ONSITE",
        durationWeeks = 4,
        applicationDeadline = DateTimeOffset.UtcNow.AddDays(30),
        expectedStudentCount = 3,
        minTeamSize = 2,
        maxTeamSize = 4,
        allowanceAmount = 2_000_000,
        allowanceCurrency = "VND",
        skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
        deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
    };

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);

    private sealed record AuthDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId);

    private sealed record CatalogSkillDto(Guid Id, string Code, string Name, string Category, string? Description, bool IsActive);

    private sealed record ProjectDetailDto(Guid Id, Guid CompanyId, string Code, string Title, string Slug, string Status, bool IsActive);

    private sealed record SkillMatchItemDto(Guid SkillId, string SkillName);

    private sealed record SkillMatchDto(Guid ProjectId, int RequiredCount, int MatchedCount, int MatchPercent, IReadOnlyCollection<SkillMatchItemDto> Matched, IReadOnlyCollection<SkillMatchItemDto> Missing);

    private sealed record SavedItemDto(Guid ProjectId, string Title, string Slug, string CompanyName, string Status, string Difficulty, int DurationWeeks, long? AllowanceAmount, string? AllowanceCurrency, DateTimeOffset SavedAt);

    private sealed record MetaDto(int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record PagedListDto(IReadOnlyCollection<SavedItemDto> Data, MetaDto Meta);
}
