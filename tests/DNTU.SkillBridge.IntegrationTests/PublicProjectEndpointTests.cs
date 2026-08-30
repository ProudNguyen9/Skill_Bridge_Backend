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
/// Task 14: anonymous public browsing only exposes active projects in publicly
/// visible statuses, with server-side filters, deterministic sorts, and detail data.
/// Every seed uses a unique title prefix so assertions stay deterministic even though
/// the collection shares one database across tests.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class PublicProjectEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";

    /// <summary>Unique ASCII prefix baked into every seeded title of this test instance.</summary>
    private readonly string prefix = $"zp{Guid.NewGuid():N}"[..10];
    private string AlphaTitle => $"{prefix} Alpha Platform";
    private string MobileTitle => $"{prefix} Mobile Suite";
    private string CoreTitle => $"{prefix} Core Portal";

    [Fact]
    public async Task List_OnlyReturnsVisibleProjects_WithPagingEnvelope()
    {
        var admin = await CreateAdminClientAsync();

        // Draft (never submitted) and pending (submitted, not approved) must stay hidden.
        var company = await CreateVerifiedCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(3);
        var draftId = await CreateProjectAsync(company, $"{prefix} draft hidden", [skillIds[0]]);
        _ = draftId;
        var pendingId = await CreateProjectAsync(company, $"{prefix} pending hidden", [skillIds[0]]);
        await MakeSubmittableAsync(company, pendingId, $"{prefix} pending hidden", skillIds[0]);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsync($"/api/v1/company/projects/{pendingId}/submit", null)).StatusCode);

        await SeedApprovedProjectsAsync(admin, skillIds);

        var page = await GetPagedListAsync($"/api/v1/projects?search={prefix}&page=1&pageSize=2");
        Assert.Equal(3, page.Meta.TotalItems);
        Assert.Equal(2, page.Data.Count);
        Assert.All(page.Data, item => Assert.StartsWith(prefix, item.Title, StringComparison.Ordinal));

        // Page 2 returns exactly the remaining row; no duplicates across pages.
        var page2 = await GetPagedListAsync($"/api/v1/projects?search={prefix}&page=2&pageSize=2");
        Assert.Single(page2.Data);
        Assert.Equal(3, page2.Meta.TotalItems);
        Assert.Equal(2, page2.Meta.TotalPages);
        Assert.DoesNotContain(page2.Data, item => page.Data.Select(row => row.Id).Contains(item.Id));
    }

    [Fact]
    public async Task Filters_Difficulty_Skill_Duration_Allowance_Search_Combine()
    {
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(3);
        await SeedApprovedProjectsAsync(admin, skillIds);

        // difficulty=ADVANCED → the two advanced seeded projects.
        var advanced = await GetPagedListAsync($"/api/v1/projects?search={prefix}&difficulty=ADVANCED");
        Assert.Equal(new[] { CoreTitle, MobileTitle }, advanced.Data.Select(item => item.Title).Order().ToArray());

        // skillId filter: s0 belongs to Alpha + Mobile; s2 only to Core.
        var withSkill0 = await GetPagedListAsync($"/api/v1/projects?search={prefix}&skillId={skillIds[0]}");
        Assert.Equal(new[] { AlphaTitle, MobileTitle }, withSkill0.Data.Select(item => item.Title).Order().ToArray());

        var withSkill2 = await GetPagedListAsync($"/api/v1/projects?search={prefix}&skillId={skillIds[2]}");
        Assert.Equal([CoreTitle], withSkill2.Data.Select(item => item.Title).ToArray());

        // duration 5..12 → Mobile (12) and Core (8), not Alpha (4).
        var duration = await GetPagedListAsync($"/api/v1/projects?search={prefix}&durationMin=5&durationMax=12");
        Assert.Equal(new[] { CoreTitle, MobileTitle }, duration.Data.Select(item => item.Title).Order().ToArray());

        // allowance >= 4,000,000 → Mobile (9,000,000) and Core (5,000,000).
        var allowance = await GetPagedListAsync($"/api/v1/projects?search={prefix}&allowanceMin=4000000");
        Assert.Equal(new[] { CoreTitle, MobileTitle }, allowance.Data.Select(item => item.Title).Order().ToArray());

        // Case-insensitive search over a lowercase title fragment.
        var search = await GetPagedListAsync($"/api/v1/projects?search={Uri.EscapeDataString($"{prefix} MOBILE")}");
        Assert.Equal([MobileTitle], search.Data.Select(item => item.Title).ToArray());
    }

    [Fact]
    public async Task Sorts_Are_Deterministic_And_UnknownSort_IsRejected()
    {
        var admin = await CreateAdminClientAsync();
        await SeedApprovedProjectsAsync(admin, await GetActiveSkillIdsAsync(3));

        var allowance = await GetPagedListAsync($"/api/v1/projects?search={prefix}&sort=allowance");
        Assert.Equal([MobileTitle, CoreTitle, AlphaTitle], allowance.Data.Select(item => item.Title).ToArray());

        var deadline = await GetPagedListAsync($"/api/v1/projects?search={prefix}&sort=deadline");
        Assert.Equal([MobileTitle, AlphaTitle, CoreTitle], deadline.Data.Select(item => item.Title).ToArray());

        var title = await GetPagedListAsync($"/api/v1/projects?search={prefix}&sort=title");
        Assert.Equal([AlphaTitle, CoreTitle, MobileTitle], title.Data.Select(item => item.Title).ToArray());

        var newest = await GetPagedListAsync($"/api/v1/projects?search={prefix}&sort=newest");
        Assert.Equal(3, newest.Data.Count);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await factory.CreateClient().GetAsync("/api/v1/projects?sort=banana")).StatusCode);
    }

    [Fact]
    public async Task Detail_BySlug_ReturnsFullData_DraftAndUnknown_Are404()
    {
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(3);

        // Draft stays invisible even though it has a slug.
        var company = await CreateVerifiedCompanyAsync();
        var draftId = await CreateProjectAsync(company, $"{prefix} draft hidden", [skillIds[0]]);
        var draftSlug = (await (await company.GetAsync($"/api/v1/company/projects/{draftId}"))
            .Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data.Slug;

        var alphaSlug = await SeedApprovedProjectsAsync(admin, skillIds);

        var response = await factory.CreateClient().GetAsync($"/api/v1/projects/{alphaSlug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<Envelope<PublicProjectDetailDto>>(Json))!.Data;

        Assert.Equal(AlphaTitle, detail.Title);
        Assert.NotEqual(Guid.Empty, detail.CompanyId);
        Assert.Contains("Công khai", detail.CompanyName, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(detail.CompanySlug));
        Assert.Equal("APPROVED", detail.Status);
        Assert.Equal(2, detail.Skills.Count);
        Assert.Contains(detail.Skills, skill => skill.RequirementLevel == "MUST_HAVE" && skill.IsRequired);
        Assert.Contains(detail.Deliverables, deliverable => deliverable.Name == "Báo cáo bàn giao");
        Assert.NotNull(detail.ProblemStatement);

        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/projects/{draftSlug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync("/api/v1/projects/khong-ton-tai-slug")).StatusCode);
    }

    [Fact]
    public async Task Related_RanksBySharedSkills_And_DraftIs404()
    {
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(3);

        var company = await CreateVerifiedCompanyAsync();
        var draftId = await CreateProjectAsync(company, $"{prefix} draft hidden", [skillIds[0]]);

        // Distinct seed: Alpha and Mobile share all 3 skills, Core shares only 2. Other tests in the
        // shared database use at most 2-skill sets, so Mobile (3) and Core (2) outrank foreign projects.
        var alphaId = await PublishProjectAsync(company, admin, $"{prefix}R Alpha", skillIds, "BEGINNER", "ONSITE", 4, 2_000_000, 30);
        var mobileId = await PublishProjectAsync(company, admin, $"{prefix}R Mobile", skillIds, "ADVANCED", "HYBRID", 12, 9_000_000, 5);
        var coreId = await PublishProjectAsync(company, admin, $"{prefix}R Core", skillIds.Skip(1).ToList(), "ADVANCED", "REMOTE", 8, 5_000_000, 60);
        _ = mobileId;
        _ = coreId;

        var related = (await (await factory.CreateClient()
                .GetAsync($"/api/v1/projects/{alphaId}/related?take=20"))
            .Content.ReadFromJsonAsync<Envelope<List<PublicListItemDto>>>(Json))!.Data;

        var mine = related.Where(item => item.Title.StartsWith($"{prefix}R ", StringComparison.Ordinal)).ToList();
        Assert.Contains(mine, item => item.Title == $"{prefix}R Mobile");
        Assert.Contains(mine, item => item.Title == $"{prefix}R Core");
        Assert.DoesNotContain(mine, item => item.Id == alphaId);
        Assert.True(
            mine.FindIndex(item => item.Title == $"{prefix}R Mobile") < mine.FindIndex(item => item.Title == $"{prefix}R Core"),
            "Projects sharing more skills must rank before projects sharing fewer.");

        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/projects/{draftId}/related")).StatusCode);
    }

    [Fact]
    public async Task Skills_Endpoint_ListsRequirements_And_Milestones_AreEmpty()
    {
        var admin = await CreateAdminClientAsync();
        await SeedApprovedProjectsAsync(admin, await GetActiveSkillIdsAsync(3));

        var mobile = (await GetPagedListAsync($"/api/v1/projects?search={prefix}"))!
            .Data.Single(item => item.Title == MobileTitle);

        var response = await factory.CreateClient().GetAsync($"/api/v1/projects/{mobile.Id}/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var skills = (await response.Content.ReadFromJsonAsync<Envelope<List<PublicSkillDto>>>(Json))!.Data;
        Assert.Equal(2, skills.Count);
        Assert.All(skills, skill =>
            Assert.Contains(skill.RequirementLevel, new[] { "NICE_TO_HAVE", "IMPORTANT", "MUST_HAVE" }));

        var milestones = await factory.CreateClient().GetAsync($"/api/v1/projects/{mobile.Id}/public-milestones");
        Assert.Equal(HttpStatusCode.OK, milestones.StatusCode);
        var payload = (await milestones.Content.ReadFromJsonAsync<Envelope<List<object>>>(Json))!.Data;
        Assert.Empty(payload);
    }

    /// <summary>
    /// Publishes (submit + admin approve) three projects across two verified companies:
    /// Alpha (BEGINNER, ONSITE, 4w, 2M VND, deadline +30d, skills s0+s1),
    /// Mobile (ADVANCED, HYBRID, 12w, 9M VND, deadline +5d, skills s0+s1),
    /// Core (ADVANCED, REMOTE, 8w, 5M VND, deadline +60d, skills s1+s2).
    /// Returns Alpha's public slug.
    /// </summary>
    private async Task<string> SeedApprovedProjectsAsync(HttpClient admin, IReadOnlyList<Guid> skillIds)
    {
        var companyA = await CreateVerifiedCompanyAsync();
        var companyB = await CreateVerifiedCompanyAsync();

        var alphaId = await PublishProjectAsync(companyA, admin, AlphaTitle, skillIds.Take(2).ToList(), "BEGINNER", "ONSITE", 4, 2_000_000, 30);
        var mobileId = await PublishProjectAsync(companyB, admin, MobileTitle, skillIds.Take(2).ToList(), "ADVANCED", "HYBRID", 12, 9_000_000, 5);
        var coreId = await PublishProjectAsync(companyA, admin, CoreTitle, skillIds.Skip(1).ToList(), "ADVANCED", "REMOTE", 8, 5_000_000, 60);
        _ = mobileId;
        _ = coreId;

        var alphaSlug = (await GetPagedListAsync($"/api/v1/projects?search={prefix}"))!
            .Data.Single(item => item.Id == alphaId).Slug;
        return alphaSlug;
    }

    private async Task<Guid> PublishProjectAsync(
        HttpClient company, HttpClient admin, string title, IReadOnlyList<Guid> skillIds,
        string difficulty, string workType, int durationWeeks, long allowance, int deadlineInDays)
    {
        var projectId = await CreateProjectAsync(company, title, skillIds);
        var updated = await company.PutAsJsonAsync($"/api/v1/company/projects/{projectId}",
            CompletePayload(title, skillIds, difficulty, workType, durationWeeks, allowance, deadlineInDays), Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);
        return projectId;
    }

    private static object CompletePayload(
        string title, IReadOnlyList<Guid> skillIds, string difficulty = "BEGINNER", string workType = "ONSITE",
        int durationWeeks = 4, long allowance = 2_000_000, int deadlineInDays = 14) => new
    {
        title,
        summary = $"Tóm tắt công khai cho {title}",
        problemStatement = "Vấn đề thực tế cần giải quyết.",
        businessRequirements = "Yêu cầu nghiệp vụ rõ ràng.",
        technicalConstraints = "Ràng buộc kỹ thuật .NET.",
        difficulty,
        workType,
        durationWeeks,
        applicationDeadline = DateTimeOffset.UtcNow.AddDays(deadlineInDays),
        expectedStudentCount = 3,
        minTeamSize = 2,
        maxTeamSize = 4,
        allowanceAmount = allowance,
        allowanceCurrency = "VND",
        skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
        deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
    };

    private async Task<PagedListDto> GetPagedListAsync(string url)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"{url} → {(int)response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<PagedListDto>(Json))!;
    }

    private async Task<HttpClient> CreateVerifiedCompanyAsync()
    {
        var client = await RegisterCompanyAsync();
        var companyName = $"Công ty Công khai {Guid.NewGuid():N}";
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
        var email = $"public-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Công khai",
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
        Assert.True(skills.Count >= count, $"Need at least {count} active skills for public project tests.");
        return skills.Take(count).Select(skill => skill.Id).ToList();
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

    private async Task MakeSubmittableAsync(HttpClient client, Guid projectId, string title, Guid skillId)
    {
        var updated = await client.PutAsJsonAsync($"/api/v1/company/projects/{projectId}",
            CompletePayload(title, [skillId]), Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);

    private sealed record AuthDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId);

    private sealed record CatalogSkillDto(Guid Id, string Code, string Name, string Category, string? Description, bool IsActive);

    private sealed record ProjectDetailDto(Guid Id, Guid CompanyId, string Code, string Title, string Slug, string Status, bool IsActive);

    private sealed record PublicSkillDto(Guid SkillId, string SkillName, string RequirementLevel, bool IsRequired);

    private sealed record PublicListItemDto(
        Guid Id, string Code, string Title, string Slug, string? Summary, string Status, string Difficulty,
        string WorkType, int DurationWeeks, DateTimeOffset? ApplicationDeadline, int ExpectedStudentCount,
        long? AllowanceAmount, string? AllowanceCurrency, string CompanyName, string CompanySlug,
        IReadOnlyCollection<string> SkillNames);

    private sealed record PublicProjectDetailDto(
        Guid Id, string Code, string Title, string Slug, string? Summary, string? ProblemStatement,
        string? BusinessRequirements, string? TechnicalConstraints, string Status, string Difficulty,
        string WorkType, int DurationWeeks, DateTimeOffset? ApplicationDeadline, DateTimeOffset CreatedAt,
        int MinTeamSize, int MaxTeamSize, int ExpectedStudentCount, long? AllowanceAmount, string? AllowanceCurrency,
        Guid CompanyId, string CompanyName, string CompanySlug, Guid? IndustryId, string? IndustryName,
        IReadOnlyCollection<PublicSkillDto> Skills, IReadOnlyCollection<PublicDeliverableDto> Deliverables);

    private sealed record PublicDeliverableDto(string Name, string? Description, int SortOrder);

    private sealed record MetaDto(int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record PagedListDto(IReadOnlyCollection<PublicListItemDto> Data, MetaDto Meta);
}
