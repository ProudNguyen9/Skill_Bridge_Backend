using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class SubmissionTechnicalReviewEndpointTests(CatalogApiFactory factory)
{
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;

    [Fact]
    public async Task Submission_endpoints_are_scoped_by_student_company_and_assigned_lecturer()
    {
        var mine = await SeedWorkflowAsync();
        var foreign = await SeedWorkflowAsync();
        var student = CreateRoleClient(RoleNames.Student, mine.StudentUserId);
        var company = CreateRoleClient(RoleNames.Company, mine.CompanyUserId);
        var lecturer = CreateRoleClient(RoleNames.Lecturer, mine.LecturerUserId);

        var create = await student.PostAsJsonAsync($"/api/v1/projects/{mine.ProjectId}/submissions", new
        {
            summary = "Evidence submitted through the HTTP endpoint.",
            githubUrl = "https://github.com/dntu/skillbridge",
            fileId = mine.CompletedFileId
        }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<Envelope<SubmissionDto>>(Json))!.Data;
        Assert.Equal(mine.ProjectId, created.ProjectId);

        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/v1/submissions/{foreign.SubmissionId}")).StatusCode);
        Assert.DoesNotContain(
            foreign.ProjectId,
            (await student.GetFromJsonAsync<ListEnvelope<SubmissionDto>>("/api/v1/students/me/submissions", Json))!.Data.Select(item => item.ProjectId));
        Assert.DoesNotContain(
            foreign.ProjectId,
            (await company.GetFromJsonAsync<ListEnvelope<SubmissionDto>>("/api/v1/company/submissions", Json))!.Data.Select(item => item.ProjectId));
        Assert.DoesNotContain(
            foreign.ProjectId,
            (await lecturer.GetFromJsonAsync<ListEnvelope<SubmissionDto>>("/api/v1/lecturer/submissions", Json))!.Data.Select(item => item.ProjectId));
    }

    [Fact]
    public async Task Lecturer_revision_request_enables_student_new_version_and_preserves_review_history()
    {
        var workflow = await SeedWorkflowAsync();
        var lecturer = CreateRoleClient(RoleNames.Lecturer, workflow.LecturerUserId);
        var student = CreateRoleClient(RoleNames.Student, workflow.StudentUserId);
        var company = CreateRoleClient(RoleNames.Company, workflow.CompanyUserId);

        var review = await lecturer.PostAsJsonAsync($"/api/v1/lecturer/submissions/{workflow.SubmissionId}/technical-review", new
        {
            version = workflow.SubmissionVersion,
            decision = "REVISION_REQUIRED",
            feedback = "Please add deployment proof and clarify the test evidence.",
            criteriaNotes = "{\"architecture\":\"needs-revision\"}"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);

        var afterReview = (await student.GetFromJsonAsync<Envelope<SubmissionDto>>($"/api/v1/submissions/{workflow.SubmissionId}", Json))!.Data;
        Assert.Equal("REVISION_REQUIRED", afterReview.Status);

        var newVersion = await student.PostAsJsonAsync($"/api/v1/submissions/{workflow.SubmissionId}/new-version", new
        {
            version = afterReview.Version,
            summary = "Updated evidence after lecturer revision.",
            demoUrl = "https://demo.skillbridge.local/revision"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, newVersion.StatusCode);
        var resubmitted = (await newVersion.Content.ReadFromJsonAsync<Envelope<SubmissionDto>>(Json))!.Data;
        Assert.Equal("RESUBMITTED", resubmitted.Status);
        Assert.Equal(2, resubmitted.CurrentVersionNumber);

        var reviews = (await lecturer.GetFromJsonAsync<ListEnvelope<TechnicalReviewDto>>($"/api/v1/submissions/{workflow.SubmissionId}/reviews", Json))!.Data;
        Assert.Single(reviews);
        Assert.Equal("REVISION_REQUIRED", reviews.Single().Decision);
        Assert.Equal(HttpStatusCode.Forbidden, (await company.GetAsync($"/api/v1/submissions/{workflow.SubmissionId}/reviews")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_student_new_version_requests_leave_one_current_version()
    {
        var workflow = await SeedWorkflowAsync();
        var lecturer = CreateRoleClient(RoleNames.Lecturer, workflow.LecturerUserId);
        var student = CreateRoleClient(RoleNames.Student, workflow.StudentUserId);
        await lecturer.PostAsJsonAsync($"/api/v1/lecturer/submissions/{workflow.SubmissionId}/technical-review", new
        {
            version = workflow.SubmissionVersion,
            decision = "REVISION_REQUIRED",
            feedback = "Revision required before final technical approval."
        }, Json);
        var token = (await student.GetFromJsonAsync<Envelope<SubmissionDto>>($"/api/v1/submissions/{workflow.SubmissionId}", Json))!.Data.Version;

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            student.PostAsJsonAsync($"/api/v1/submissions/{workflow.SubmissionId}/new-version", new
            {
                version = token,
                summary = "Concurrent resubmission evidence.",
                videoUrl = "https://video.skillbridge.local/resubmission"
            }, Json)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.Equal(2, await dbContext.SubmissionVersions.CountAsync(item => item.SubmissionId == workflow.SubmissionId));
            Assert.Equal(2, await dbContext.ProjectSubmissions.Where(item => item.Id == workflow.SubmissionId).Select(item => item.CurrentVersionNumber).SingleAsync());
        });
    }

    [Fact]
    public async Task Only_assigned_lecturer_can_review_and_parallel_reviews_conflict_cleanly()
    {
        var workflow = await SeedWorkflowAsync();
        var assigned = CreateRoleClient(RoleNames.Lecturer, workflow.LecturerUserId);
        var outsider = CreateRoleClient(RoleNames.Lecturer, (await SeedLecturerAsync()).UserId);

        var forbidden = await outsider.PostAsJsonAsync($"/api/v1/lecturer/submissions/{workflow.SubmissionId}/technical-review", TechnicalReviewPayload(workflow.SubmissionVersion), Json);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            assigned.PostAsJsonAsync($"/api/v1/lecturer/submissions/{workflow.SubmissionId}/technical-review", TechnicalReviewPayload(workflow.SubmissionVersion), Json)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.Equal(1, await dbContext.TechnicalSubmissionReviews.CountAsync(item => item.SubmissionId == workflow.SubmissionId));
            Assert.Equal(SubmissionStatus.TECHNICAL_APPROVED, await dbContext.ProjectSubmissions.Where(item => item.Id == workflow.SubmissionId).Select(item => item.Status).SingleAsync());
            Assert.True(await dbContext.ProjectActivities.AnyAsync(item => item.ProjectId == workflow.ProjectId && item.EventType == "SUBMISSION_TECHNICALLY_APPROVED"));
            Assert.True(await dbContext.OutboxMessages.AnyAsync(item => item.Type == "submission.technical-review.completed"));
        });
    }

    private async Task<SeededWorkflow> SeedWorkflowAsync()
    {
        SeededWorkflow? result = null;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var studentUser = await AddUserAsync(dbContext, RoleNames.Student, "submission-student");
            var companyUser = await AddUserAsync(dbContext, RoleNames.Company, "submission-company");
            var lecturer = await SeedLecturerAsync(dbContext);
            var student = new StudentProfile(studentUser.Id);
            var company = new Company($"Submission Company {Guid.NewGuid():N}");
            dbContext.StudentProfiles.Add(student);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();

            dbContext.CompanyMembers.Add(new CompanyMember(company.Id, companyUser.Id, CompanyMemberRole.OWNER, "Owner"));
            var project = new Project(company.Id, $"PRJ-{Guid.NewGuid():N}"[..12], $"Submission Project {Guid.NewGuid():N}", ProjectDifficulty.BEGINNER, ProjectWorkType.REMOTE, 4, 3, 1, 4, null, null, null, null, null, null, null, null);
            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync();

            dbContext.ProjectMembers.Add(new ProjectMember(project.Id, student.Id));
            var assignment = new LecturerAssignment(lecturer.ProfileId, project.Id, LecturerAssignmentRole.PRIMARY, "Primary supervisor");
            assignment.Accept(DateTimeOffset.UtcNow);
            dbContext.LecturerAssignments.Add(assignment);
            var file = new FileRecord(studentUser.Id, project.Id, "evidence.zip", $"submissions/{Guid.NewGuid():N}.zip", "application/zip", 1024, DateTimeOffset.UtcNow.AddHours(1));
            file.Complete(new string('a', 64), DateTimeOffset.UtcNow);
            dbContext.FileRecords.Add(file);
            var submission = new ProjectSubmission(project.Id, null, student.Id, new SubmissionVersion("Initial evidence", "https://github.com/dntu/skillbridge", null, null, file.Id));
            dbContext.ProjectSubmissions.Add(submission);
            await dbContext.SaveChangesAsync();

            result = new SeededWorkflow(
                project.Id,
                studentUser.Id,
                student.Id,
                companyUser.Id,
                lecturer.UserId,
                submission.Id,
                submission.Version,
                file.Id);
        });

        return result!;
    }

    private async Task<SeededLecturer> SeedLecturerAsync()
    {
        SeededLecturer? result = null;
        await factory.RunWithDbContextAsync(async dbContext => result = await SeedLecturerAsync(dbContext));
        return result!;
    }

    private static async Task<SeededLecturer> SeedLecturerAsync(Infrastructure.Persistence.AppDbContext dbContext)
    {
        var user = await AddUserAsync(dbContext, RoleNames.Lecturer, "submission-lecturer");
        var profile = new LecturerProfile(user.Id);
        dbContext.LecturerProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return new SeededLecturer(user.Id, profile.Id);
    }

    private static async Task<User> AddUserAsync(Infrastructure.Persistence.AppDbContext dbContext, string roleName, string prefix)
    {
        var user = new User($"{prefix}-{Guid.NewGuid():N}@skillbridge.local", "Endpoint Test User", "not-a-real-hash");
        var role = await dbContext.Roles.SingleAsync(role => role.NormalizedName == roleName);
        user.AssignRole(role.Id);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private HttpClient CreateRoleClient(string role, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(role, userId));
        return client;
    }

    private static object TechnicalReviewPayload(Guid version) => new
    {
        version,
        decision = "APPROVED",
        feedback = "Technically approved with required evidence verified.",
        criteriaNotes = "{\"quality\":\"approved\"}"
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

    private sealed record SeededWorkflow(Guid ProjectId, Guid StudentUserId, Guid StudentId, Guid CompanyUserId, Guid LecturerUserId, Guid SubmissionId, Guid SubmissionVersion, Guid CompletedFileId);
    private sealed record SeededLecturer(Guid UserId, Guid ProfileId);
    private sealed record Envelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);
    private sealed record SubmissionDto(Guid Id, Guid ProjectId, Guid? MilestoneId, string Status, int CurrentVersionNumber, Guid SubmittedByStudentId, Guid Version, DateTimeOffset CreatedAt);
    private sealed record TechnicalReviewDto(Guid Id, Guid SubmissionId, Guid SubmissionVersionId, Guid ReviewerUserId, string Decision, string Feedback, string? CriteriaNotes, DateTimeOffset CreatedAt);
}
