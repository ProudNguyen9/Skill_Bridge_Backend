using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class BusinessReviewEndpointTests(CatalogApiFactory factory)
{
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;

    [Fact]
    public async Task Owning_company_can_accept_technically_approved_current_version_and_writes_audit_outbox()
    {
        var reviewer = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var submission = await SeedTechnicallyApprovedSubmissionAsync(projectId, companyId, reviewer);
        var client = CreateCompanyClient(submission.ReviewerUserId);

        var response = await client.PostAsJsonAsync($"/api/v1/company/submissions/{submission.Id}/business-review", new
        {
            version = submission.Version,
            decision = "ACCEPTED",
            requirementsFeedback = "Đáp ứng yêu cầu nghiệp vụ.",
            collaborationFeedback = "Bàn giao đúng hạn."
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = (await response.Content.ReadFromJsonAsync<Envelope<BusinessReviewDto>>(Json))!.Data;
        Assert.Equal(submission.Id, payload.SubmissionId);
        Assert.Equal("ACCEPTED", payload.Decision);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var actualProjectId = await dbContext.ProjectSubmissions.Where(item => item.Id == submission.Id).Select(item => item.ProjectId).SingleAsync();
            Assert.Equal(SubmissionStatus.BUSINESS_ACCEPTED, await dbContext.ProjectSubmissions.Where(item => item.Id == submission.Id).Select(item => item.Status).SingleAsync());
            Assert.True(await dbContext.BusinessSubmissionReviews.AnyAsync(item => item.Id == payload.Id && item.SubmissionVersionId == submission.CurrentVersionId));
            Assert.True(await dbContext.SubmissionStatusHistories.AnyAsync(item => item.SubmissionId == submission.Id && item.ToStatus == SubmissionStatus.BUSINESS_ACCEPTED && item.ActorUserId == submission.ReviewerUserId));
            Assert.True(await dbContext.ProjectActivities.AnyAsync(item => item.ProjectId == actualProjectId && item.EventType == "SUBMISSION_BUSINESS_ACCEPTED" && item.ActorUserId == submission.ReviewerUserId));
            Assert.True(await dbContext.OutboxMessages.AnyAsync(item => item.Type == "submission.business-review.completed" && item.PayloadJson.Contains(payload.Id.ToString())));
        });
    }

    [Fact]
    public async Task Foreign_company_and_company_member_without_review_authority_are_forbidden()
    {
        var owner = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var submission = await SeedTechnicallyApprovedSubmissionAsync(projectId, companyId, owner);
        var outsider = CreateCompanyClient(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync($"/api/v1/company/submissions/{submission.Id}/business-review", ReviewPayload(submission.Version), Json)).StatusCode);

        var memberId = await SeedUserAsync();
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var actualCompanyId = await dbContext.ProjectSubmissions
                .Where(item => item.Id == submission.Id)
                .Join(dbContext.Projects,
                    submission => submission.ProjectId,
                    project => project.Id,
                    (submission, project) => project.CompanyId)
                .SingleAsync();
            dbContext.CompanyMembers.Add(new CompanyMember(actualCompanyId, memberId, CompanyMemberRole.MEMBER, "Quan sát"));
            await dbContext.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateCompanyClient(memberId).PostAsJsonAsync($"/api/v1/company/submissions/{submission.Id}/business-review", ReviewPayload(submission.Version), Json)).StatusCode);
    }

    [Fact]
    public async Task Business_review_requires_technical_approval_current_token_and_revision_allows_new_version()
    {
        var reviewer = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var pending = await SeedSubmissionAsync(projectId, companyId, reviewer, technicallyApprove: false);
        var client = CreateCompanyClient(pending.ReviewerUserId);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/company/submissions/{pending.Id}/business-review", ReviewPayload(pending.Version), Json)).StatusCode);

        var approved = await SeedTechnicallyApprovedSubmissionAsync(Guid.NewGuid(), Guid.NewGuid(), reviewer);
        var approvedClient = CreateCompanyClient(approved.ReviewerUserId);
        Assert.Equal(HttpStatusCode.Conflict, (await approvedClient.PostAsJsonAsync($"/api/v1/company/submissions/{approved.Id}/business-review", ReviewPayload(Guid.NewGuid()), Json)).StatusCode);

        var revision = await approvedClient.PutAsJsonAsync($"/api/v1/company/submissions/{approved.Id}/business-review", new
        {
            version = approved.Version,
            decision = "REVISION_REQUIRED",
            requirementsFeedback = "Thiếu một số luồng nghiệp vụ.",
            collaborationFeedback = "Bổ sung hướng dẫn vận hành trước khi bàn giao lại."
        }, Json);
        Assert.Equal(HttpStatusCode.Created, revision.StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.Equal(
                SubmissionStatus.REVISION_REQUIRED,
                await dbContext.ProjectSubmissions
                    .Where(item => item.Id == approved.Id)
                    .Select(item => item.Status)
                    .SingleAsync());
        });
    }

    [Fact]
    public async Task Student_and_lecturer_cannot_create_business_review()
    {
        var reviewer = Guid.NewGuid();
        var submission = await SeedTechnicallyApprovedSubmissionAsync(Guid.NewGuid(), Guid.NewGuid(), reviewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateRoleClient(RoleNames.Student, Guid.NewGuid()).PostAsJsonAsync($"/api/v1/company/submissions/{submission.Id}/business-review", ReviewPayload(submission.Version), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateRoleClient(RoleNames.Lecturer, Guid.NewGuid()).PostAsJsonAsync($"/api/v1/company/submissions/{submission.Id}/business-review", ReviewPayload(submission.Version), Json)).StatusCode);
    }

    private async Task<SeededSubmission> SeedTechnicallyApprovedSubmissionAsync(Guid projectId, Guid companyId, Guid reviewerUserId) =>
        await SeedSubmissionAsync(projectId, companyId, reviewerUserId, technicallyApprove: true);

    private async Task<SeededSubmission> SeedSubmissionAsync(Guid projectId, Guid companyId, Guid reviewerUserId, bool technicallyApprove)
    {
        SeededSubmission? result = null;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var reviewer = new User($"business-review-{Guid.NewGuid():N}@skillbridge.local", "Đại diện nghiệm thu", "not-a-real-hash");
            dbContext.Users.Add(reviewer);
            var company = new Company($"Công ty review {Guid.NewGuid():N}");
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
            reviewerUserId = reviewer.Id;
            companyId = company.Id;
            dbContext.CompanyMembers.Add(new CompanyMember(companyId, reviewerUserId, CompanyMemberRole.OWNER, "Đại diện nghiệm thu"));

            var project = new Project(companyId, $"PRJ-{Guid.NewGuid():N}"[..12], $"Dự án nghiệm thu {Guid.NewGuid():N}", ProjectDifficulty.BEGINNER, ProjectWorkType.REMOTE, 4, 3, 1, 4, null, null, null, null, null, null, null, null);
            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync();
            projectId = project.Id;

            var submission = new ProjectSubmission(projectId, null, Guid.NewGuid(), new SubmissionVersion("Bằng chứng bàn giao", null, null, null, null));
            if (technicallyApprove)
            {
                submission.ApproveTechnical(submission.Version);
            }
            dbContext.ProjectSubmissions.Add(submission);
            await dbContext.SaveChangesAsync();
            result = new SeededSubmission(submission.Id, submission.Version, submission.Versions.Single().Id, reviewerUserId);
        });
        return result!;
    }

    private async Task<Guid> SeedUserAsync()
    {
        var userId = Guid.Empty;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new User($"business-member-{Guid.NewGuid():N}@skillbridge.local", "Thành viên công ty", "not-a-real-hash");
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            userId = user.Id;
        });
        return userId;
    }

    private HttpClient CreateCompanyClient(Guid userId) => CreateRoleClient(RoleNames.Company, userId);

    private HttpClient CreateRoleClient(string role, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(role, userId));
        return client;
    }

    private static object ReviewPayload(Guid version) => new
    {
        version,
        decision = "ACCEPTED",
        requirementsFeedback = "Đạt yêu cầu.",
        collaborationFeedback = "Đạt."
    };

    private static string CreateToken(string role, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record SeededSubmission(Guid Id, Guid Version, Guid CurrentVersionId, Guid ReviewerUserId);
    private sealed record Envelope<T>(T Data);
    private sealed record BusinessReviewDto(Guid Id, Guid SubmissionId, Guid SubmissionVersionId, Guid CompanyId, Guid ReviewerUserId, string Decision, string RequirementsFeedback, string CollaborationFeedback, DateTimeOffset CreatedAt);
}
