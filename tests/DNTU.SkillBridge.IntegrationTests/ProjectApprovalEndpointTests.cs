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

[Collection(CatalogApiCollection.Name)]
public sealed class ProjectApprovalEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";

    [Fact]
    public async Task Submit_IncompleteDraft_IsRejected_ThenCompleteDraft_IsAccepted()
    {
        var client = await CreateVerifiedCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);
        var projectId = await CreateProjectAsync(client, "Dự án chưa đầy đủ");

        // Incomplete: no summary/problem statement, no deadline.
        var incomplete = await client.PostAsync($"/api/v1/company/projects/{projectId}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode);

        await MakeSubmittableAsync(client, projectId, "Dự án đầy đủ", skillIds[0]);

        var submitted = await client.PostAsync($"/api/v1/company/projects/{projectId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        var payload = (await submitted.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data;
        Assert.Equal("PENDING_APPROVAL", payload.Status);
    }

    [Fact]
    public async Task Submit_UnverifiedCompany_OrPastDeadline_IsRejected()
    {
        var unverified = await CreateCompanyAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);
        var projectId = await CreateProjectAsync(unverified, "Dự án công ty chưa xác minh");
        await MakeSubmittableAsync(unverified, projectId, "Dự án công ty chưa xác minh", skillIds[0]);
        Assert.Equal(HttpStatusCode.Conflict,
            (await unverified.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);

        var verified = await CreateVerifiedCompanyAsync();
        var pastDeadlineId = await CreateProjectAsync(verified, "Dự án hạn đã qua");
        await MakeSubmittableAsync(verified, pastDeadlineId, "Dự án hạn đã qua", skillIds[0], DateTimeOffset.UtcNow.AddDays(-2));
        Assert.Equal(HttpStatusCode.Conflict,
            (await verified.PostAsync($"/api/v1/company/projects/{pastDeadlineId}/submit", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_Approve_SetsStatus_And_HistoryPersists()
    {
        var client = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var projectId = await SubmitProjectAsync(client, "Dự án được duyệt");

        var before = (await admin.GetFromJsonAsync<Envelope<ApprovalDetailDto>>(
            $"/api/v1/admin/project-approvals/{projectId}", Json))!.Data;
        Assert.Equal("PENDING_APPROVAL", before.Status);
        Assert.Equal(new[] { "SUBMITTED" }, before.History.Select(item => item.Decision).ToArray());

        var approved = await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { note = (string?)"Đạt yêu cầu" }, Json);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var detail = (await approved.Content.ReadFromJsonAsync<Envelope<ApprovalDetailDto>>(Json))!.Data;
        Assert.Equal("APPROVED", detail.Status);
        Assert.Equal(["SUBMITTED", "APPROVED"], detail.History.Select(item => item.Decision).ToArray());
        Assert.Equal("Đạt yêu cầu", detail.History.Last().Note);
        Assert.NotNull(detail.ApprovedAt);
        Assert.NotNull(detail.PublishedAt);
        Assert.Equal(1, detail.SkillCount);

        var queue = (await admin.GetFromJsonAsync<ApprovalListDto>(
            "/api/v1/admin/project-approvals?status=PENDING_APPROVAL&page=1&pageSize=50", Json))!;
        Assert.DoesNotContain(queue.Data, item => item.ProjectId == projectId);
    }

    [Fact]
    public async Task Company_CannotUseAdminEndpoints_OrTouchForeignProjects()
    {
        var companyA = await CreateVerifiedCompanyAsync();
        var companyB = await CreateVerifiedCompanyAsync();

        // Company role on the admin approval queue and admin decision endpoints → 403.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await companyA.GetAsync("/api/v1/admin/project-approvals")).StatusCode);

        var projectIdB = await CreateProjectAsync(companyB, "Dự án công ty B");
        await MakeSubmittableAsync(companyB, projectIdB, "Dự án công ty B", (await GetActiveSkillIdsAsync(1))[0]);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await companyA.PostAsync($"/api/v1/admin/projects/{projectIdB}/approve", null)).StatusCode);

        // Company A acting on company B's project → 404 (tenant isolation).
        Assert.Equal(HttpStatusCode.NotFound, (await companyA.PostAsync($"/api/v1/company/projects/{projectIdB}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await companyA.PostAsync($"/api/v1/company/projects/{projectIdB}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await companyA.PostAsync($"/api/v1/company/projects/{projectIdB}/reopen", null)).StatusCode);
    }

    [Fact]
    public async Task InvalidTransitions_Return409()
    {
        var client = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var projectId = await SubmitProjectAsync(client, "Dự án chuyển trạng thái sai");

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);

        // Approve twice.
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);

        // Request changes after approval.
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/request-changes", new { }, Json)).StatusCode);
    }

    [Fact]
    public async Task Reject_ThenReopen_AllowsEditsAgain()
    {
        var client = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var skillIds = await GetActiveSkillIdsAsync(2);
        var projectId = await SubmitProjectAsync(client, "Dự án bị từ chối", skillIds[0]);

        var rejected = await admin.PostAsJsonAsync(
            $"/api/v1/admin/projects/{projectId}/reject",
            new { note = "Thiếu mô tả kỹ thuật" }, Json);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var detail = (await rejected.Content.ReadFromJsonAsync<Envelope<ApprovalDetailDto>>(Json))!.Data;
        Assert.Equal("REJECTED", detail.Status);
        Assert.Equal("Thiếu mô tả kỹ thuật", detail.History.Last().Note);

        // While rejected, edits are blocked.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(
            $"/api/v1/company/projects/{projectId}", CompletePayload("Dự án bị từ chối (sửa)", skillIds[0]), Json)).StatusCode);

        var reopened = await client.PostAsync($"/api/v1/company/projects/{projectId}/reopen", null);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        var reopenedPayload = (await reopened.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data;
        Assert.Equal("DRAFT", reopenedPayload.Status);

        var edited = await client.PutAsJsonAsync(
            $"/api/v1/company/projects/{projectId}", CompletePayload("Dự án mở lại hoàn chỉnh", skillIds[1]), Json);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }

    [Fact]
    public async Task Suspend_ThenResume_RestoresVisibilityStatus()
    {
        var client = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var projectId = await SubmitProjectAsync(client, "Dự án tạm dừng");

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);

        var suspended = await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/suspend", new { note = "Tạm dừng" }, Json);
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        Assert.Equal("SUSPENDED", (await suspended.Content.ReadFromJsonAsync<Envelope<ApprovalDetailDto>>(Json))!.Data.Status);

        var resumed = await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/resume", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal("APPROVED", (await resumed.Content.ReadFromJsonAsync<Envelope<ApprovalDetailDto>>(Json))!.Data.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admin_Search_MatchesTitleOrCompany_CaseInsensitively(bool searchCompany)
    {
        string companyName = string.Empty;
        var company = await CreateVerifiedCompanyAsync(name => companyName = name);
        var admin = await CreateAdminClientAsync();
        var marker = $"QueueSearch-{Guid.NewGuid():N}-MiXeD";
        var projectId = await SubmitProjectAsync(company, $"Before {marker} after");
        var otherCompany = await CreateVerifiedCompanyAsync();
        await SubmitProjectAsync(otherCompany, "Unrelated approval project");
        var draftId = await CreateProjectAsync(company, marker);
        var search = searchCompany ? companyName[4..^4] : marker;

        foreach (var term in new[] { search.ToLowerInvariant(), search.ToUpperInvariant(), $"  {search}  " })
        {
            var result = (await admin.GetFromJsonAsync<ApprovalListDto>(
                $"/api/v1/admin/project-approvals?search={Uri.EscapeDataString(term)}", Json))!;

            Assert.Equal(projectId, Assert.Single(result.Data).ProjectId);
            Assert.DoesNotContain(result.Data, item => item.ProjectId == draftId);
            Assert.Equal(new MetaDto(1, 20, 1, 1), result.Meta);
        }
    }

    [Fact]
    public async Task Admin_Search_FiltersBeforePaging_PreservesStatusAndStableMetadata()
    {
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var marker = $"QueuePaging-{Guid.NewGuid():N}";
        var projectIds = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            projectIds.Add(await SubmitProjectAsync(company, $"{marker} {index}"));
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(
            $"/api/v1/admin/projects/{projectIds[0]}/approve", new { }, Json)).StatusCode);
        var draftId = await CreateProjectAsync(company, $"{marker} draft");
        await SubmitProjectAsync(company, "Newest nonmatching project must not consume a search page");

        // Equal submission times exercise the existing deterministic project-Id tiebreak.
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var projects = await dbContext.Projects.Where(project => projectIds.Contains(project.Id)).ToListAsync();
            var submittedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            foreach (var project in projects)
            {
                dbContext.Entry(project).Property(item => item.SubmittedAt).CurrentValue = submittedAt;
            }
            await dbContext.SaveChangesAsync();
        });

        var url = $"/api/v1/admin/project-approvals?search={Uri.EscapeDataString(marker)}";
        var all = (await admin.GetFromJsonAsync<ApprovalListDto>($"{url}&pageSize=100", Json))!;
        Assert.Equal(new MetaDto(1, 100, 3, 1), all.Meta);
        Assert.Equal(projectIds.Order(), all.Data.Select(item => item.ProjectId).Order());
        Assert.DoesNotContain(all.Data, item => item.ProjectId == draftId);

        for (var page = 1; page <= 4; page++)
        {
            var result = (await admin.GetFromJsonAsync<ApprovalListDto>($"{url}&page={page}&pageSize=1", Json))!;
            Assert.Equal(new MetaDto(page, 1, 3, 3), result.Meta);
            Assert.Equal(all.Data.Skip(page - 1).Take(1), result.Data);
            var repeated = (await admin.GetFromJsonAsync<ApprovalListDto>($"{url}&page={page}&pageSize=1", Json))!;
            Assert.Equal(result.Data, repeated.Data);
            Assert.Equal(result.Meta, repeated.Meta);
        }

        var approved = (await admin.GetFromJsonAsync<ApprovalListDto>($"{url}&status=APPROVED", Json))!;
        Assert.Equal(projectIds[0], Assert.Single(approved.Data).ProjectId);
        Assert.Equal(new MetaDto(1, 20, 1, 1), approved.Meta);
        var pending = (await admin.GetFromJsonAsync<ApprovalListDto>($"{url}&status=PENDING_APPROVAL&pageSize=1", Json))!;
        Assert.Equal("PENDING_APPROVAL", Assert.Single(pending.Data).Status);
        Assert.Equal(new MetaDto(1, 1, 2, 2), pending.Meta);
        var drafts = (await admin.GetFromJsonAsync<ApprovalListDto>($"{url}&status=DRAFT", Json))!;
        Assert.Empty(drafts.Data);
        Assert.Equal(new MetaDto(1, 20, 0, 0), drafts.Meta);
    }

    [Fact]
    public async Task Admin_Search_WhitespaceIsIgnored_AndNoMatchHasEmptyMetadata()
    {
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        await SubmitProjectAsync(company, $"Whitespace baseline {Guid.NewGuid():N}");
        var baseline = (await admin.GetFromJsonAsync<ApprovalListDto>(
            "/api/v1/admin/project-approvals?status=PENDING_APPROVAL&pageSize=100", Json))!;
        var whitespace = (await admin.GetFromJsonAsync<ApprovalListDto>(
            "/api/v1/admin/project-approvals?status=PENDING_APPROVAL&pageSize=100&search=%20%20%09%20", Json))!;

        Assert.Equal(baseline.Data, whitespace.Data);
        Assert.Equal(baseline.Meta, whitespace.Meta);

        var noMatch = (await admin.GetFromJsonAsync<ApprovalListDto>(
            $"/api/v1/admin/project-approvals?search=NoMatch-{Guid.NewGuid():N}&page=2&pageSize=5", Json))!;
        Assert.Empty(noMatch.Data);
        Assert.Equal(new MetaDto(2, 5, 0, 0), noMatch.Meta);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task Admin_List_InvalidPaging_Returns400(int page, int pageSize)
    {
        var admin = await CreateAdminClientAsync();
        var response = await admin.GetAsync(
            $"/api/v1/admin/project-approvals?search=test&page={page}&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_MissingApprovalDetail_Returns404()
    {
        var admin = await CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/admin/project-approvals/{Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/project-approvals?search=test")]
    [InlineData("GET", "/api/v1/admin/project-approvals/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/admin/projects/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("POST", "/api/v1/admin/projects/00000000-0000-0000-0000-000000000001/request-changes")]
    [InlineData("POST", "/api/v1/admin/projects/00000000-0000-0000-0000-000000000001/reject")]
    [InlineData("POST", "/api/v1/admin/projects/00000000-0000-0000-0000-000000000001/suspend")]
    [InlineData("POST", "/api/v1/admin/projects/00000000-0000-0000-0000-000000000001/resume")]
    public async Task Admin_Endpoints_WithoutAuthentication_Return401(string method, string path)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { }, options: Json);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task RequestChanges_ThenResubmit_PersistsStatusNoteAndAppendOnlyHistory()
    {
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var projectId = await SubmitProjectAsync(company, "Request changes and resubmit");
        const string note = "Please clarify the business requirements.";
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(
            $"/api/v1/admin/projects/{projectId}/request-changes", new { note }, Json)).StatusCode);

        // A separate GET reloads persisted state rather than trusting the action response.
        var changed = (await admin.GetFromJsonAsync<Envelope<ApprovalDetailDto>>(
            $"/api/v1/admin/project-approvals/{projectId}", Json))!.Data;
        Assert.Equal("CHANGES_REQUESTED", changed.Status);
        Assert.Equal(["SUBMITTED", "CHANGES_REQUESTED"], changed.History.Select(item => item.Decision).ToArray());
        Assert.Equal(note, changed.History.Last().Note);
        Assert.Null(changed.ApprovedAt);
        Assert.Null(changed.PublishedAt);

        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);
        var resubmitted = (await admin.GetFromJsonAsync<Envelope<ApprovalDetailDto>>(
            $"/api/v1/admin/project-approvals/{projectId}", Json))!.Data;
        Assert.Equal("PENDING_APPROVAL", resubmitted.Status);
        Assert.Equal(["SUBMITTED", "CHANGES_REQUESTED", "SUBMITTED"],
            resubmitted.History.Select(item => item.Decision).ToArray());
        Assert.Equal(changed.History, resubmitted.History.Take(2));
        Assert.NotNull(resubmitted.SubmittedAt);
        Assert.True(resubmitted.SubmittedAt >= changed.SubmittedAt);
    }

    private async Task<HttpClient> CreateCompanyAsync()
    {
        var client = await RegisterCompanyAsync();
        var created = await client.PutAsJsonAsync("/api/v1/companies/me",
            new { name = $"Công ty Duyệt {Guid.NewGuid():N}" }, Json);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return client;
    }

    private async Task<HttpClient> CreateVerifiedCompanyAsync(Action<string>? setCompanyName = null)
    {
        var client = await RegisterCompanyAsync();
        var companyName = $"Công ty Đã xác minh {Guid.NewGuid():N}"; setCompanyName?.Invoke(companyName);
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
        var email = $"approval-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Kiểm duyệt",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Seeds a real admin user (the approval history row needs a FK-valid DecidedByUserId) and crafts a role JWT.</summary>
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
        foreach (var permission in DNTU.SkillBridge.Infrastructure.Persistence.Seed.IdentitySeed.GetPermissionsForRole(role))
        {
            claims.Add(new Claim("permission", permission));
        }

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
        return skills.Take(count).Select(skill => skill.Id).ToList();
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client, string title)
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
            skills = Array.Empty<object>(),
            deliverables = Array.Empty<object>()
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data.Id;
    }

    private static object CompletePayload(string title, Guid skillId, DateTimeOffset? deadline = null) => new
    {
        title,
        summary = "Tóm tắt đầy đủ cho quy trình duyệt",
        problemStatement = "Vấn đề thực tế cần giải quyết.",
        businessRequirements = "Yêu cầu nghiệp vụ rõ ràng.",
        technicalConstraints = "Ràng buộc kỹ thuật .NET.",
        difficulty = "INTERMEDIATE",
        workType = "HYBRID",
        durationWeeks = 8,
        applicationDeadline = deadline ?? DateTimeOffset.UtcNow.AddDays(14),
        expectedStudentCount = 3,
        minTeamSize = 2,
        maxTeamSize = 4,
        allowanceAmount = 3_000_000,
        allowanceCurrency = "VND",
        skills = new[] { new { skillId, requirementLevel = "MUST_HAVE", isRequired = true } },
        deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
    };

    private async Task MakeSubmittableAsync(HttpClient client, Guid projectId, string title, Guid skillId, DateTimeOffset? deadline = null)
    {
        var updated = await client.PutAsJsonAsync($"/api/v1/company/projects/{projectId}",
            CompletePayload(title, skillId, deadline), Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    private async Task<Guid> SubmitProjectAsync(HttpClient client, string title, Guid? skillId = null)
    {
        var effectiveSkillId = skillId ?? (await GetActiveSkillIdsAsync(1))[0];
        var projectId = await CreateProjectAsync(client, title);
        await MakeSubmittableAsync(client, projectId, title, effectiveSkillId);
        var submitted = await client.PostAsync($"/api/v1/company/projects/{projectId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        return projectId;
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

    private sealed record HistoryItem(Guid Id, string Decision, string? Note, Guid DecidedByUserId, DateTimeOffset DecidedAt);

    private sealed record ApprovalDetailDto(
        Guid ProjectId, string Title, string Slug, string CompanyName, string Status, string Code,
        string? Summary, string? ProblemStatement, string Difficulty, string WorkType, int DurationWeeks,
        DateTimeOffset? ApplicationDeadline, int ExpectedStudentCount, int MinTeamSize, int MaxTeamSize,
        long? AllowanceAmount, string? AllowanceCurrency, int SkillCount, int DeliverableCount,
        DateTimeOffset? SubmittedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? PublishedAt,
        IReadOnlyCollection<HistoryItem> History);

    private sealed record ApprovalListDto(IReadOnlyCollection<ApprovalListItemDto> Data, MetaDto Meta);

    private sealed record ApprovalListItemDto(Guid ProjectId, string Title, string CompanyName, string Status, DateTimeOffset? SubmittedAt, string? LastDecision, DateTimeOffset? LastDecidedAt);

    private sealed record MetaDto(int Page, int PageSize, int TotalItems, int TotalPages);
}
