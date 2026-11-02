using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class Milestone3EmpiricalChallengeTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";

    [Fact]
    public async Task Kanban_MoveTask_Returns409Conflict_WhenVersionIsStale_OrMismatch()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, "Kanban Conflict Test");
        var member = await RegisterStudentAsync();
        await AcceptAndConfirmAsync(company, member, projectId);

        // 1. Create a task as member
        var createRes = await member.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Task for Concurrency Challenge",
            priority = "MEDIUM",
            dueAt = DateTimeOffset.UtcNow.AddDays(7)
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var task = (await createRes.Content.ReadFromJsonAsync<Envelope<TaskDto>>(Json))!.Data;

        // 2. Move task with correct version -> Success 200 OK
        var moveRes = await member.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new
        {
            status = "IN_PROGRESS",
            sortOrder = 0,
            version = task.Version
        }, Json);
        Assert.Equal(HttpStatusCode.OK, moveRes.StatusCode);
        var movedTask = (await moveRes.Content.ReadFromJsonAsync<Envelope<TaskDto>>(Json))!.Data;
        Assert.NotEqual(task.Version, movedTask.Version);

        // 3. Attempt to move again with stale version -> Must return 409 Conflict
        var staleMoveRes = await member.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new
        {
            status = "REVIEW",
            sortOrder = 1,
            version = task.Version // Stale version
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, staleMoveRes.StatusCode);

        // 4. Verify in DB that status remains IN_PROGRESS, not REVIEW
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var dbTask = await dbContext.ProjectTasks.SingleAsync(t => t.Id == task.Id);
            Assert.Equal(ProjectTaskStatus.IN_PROGRESS, dbTask.Status);
            Assert.Equal(movedTask.Version, dbTask.Version);
        });
    }

    [Fact]
    public async Task Kanban_MultiRoleAccess_Company_Lecturer_Admin_CanAccessBoard_And_ManageTasks()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, "Multi-role Board Test");
        var member = await RegisterStudentAsync();
        await AcceptAndConfirmAsync(company, member, projectId);

        // Setup Lecturer
        var (lecturer, lecturerProfileId) = await CreateLecturerClientAsync();
        var assignRes = await admin.PostAsJsonAsync($"/api/v1/lecturers/{lecturerProfileId}/assignments", new
        {
            projectId,
            role = "SUPERVISOR"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, assignRes.StatusCode);

        var assignments = await lecturer.GetFromJsonAsync<ListEnvelope<AssignmentDto>>("/api/v1/lecturers/me/assignments", Json);
        var myAssignment = assignments!.Data.Single(a => a.ProjectId == projectId);
        var acceptRes = await lecturer.PutAsync($"/api/v1/lecturers/me/assignments/{myAssignment.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);

        // --- 1. COMPANY ROLE TEST ---
        // Company can access board
        var companyBoardRes = await company.GetAsync($"/api/v1/projects/{projectId}/board");
        Assert.Equal(HttpStatusCode.OK, companyBoardRes.StatusCode);

        // Company can create task
        var companyCreateRes = await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Task created by Company Mentor",
            priority = "HIGH"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, companyCreateRes.StatusCode);
        var companyTask = (await companyCreateRes.Content.ReadFromJsonAsync<Envelope<TaskDto>>(Json))!.Data;

        // Company can move task
        var companyMoveRes = await company.PostAsJsonAsync($"/api/v1/tasks/{companyTask.Id}/move", new
        {
            status = "IN_PROGRESS",
            sortOrder = 0,
            version = companyTask.Version
        }, Json);
        Assert.Equal(HttpStatusCode.OK, companyMoveRes.StatusCode);

        // --- 2. LECTURER ROLE TEST ---
        // Lecturer can access board
        var lecturerBoardRes = await lecturer.GetAsync($"/api/v1/projects/{projectId}/board");
        Assert.Equal(HttpStatusCode.OK, lecturerBoardRes.StatusCode);

        // Lecturer can create task
        var lecturerCreateRes = await lecturer.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Task created by Academic Lecturer",
            priority = "URGENT"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, lecturerCreateRes.StatusCode);
        var lecturerTask = (await lecturerCreateRes.Content.ReadFromJsonAsync<Envelope<TaskDto>>(Json))!.Data;

        // Lecturer can move task
        var lecturerMoveRes = await lecturer.PostAsJsonAsync($"/api/v1/tasks/{lecturerTask.Id}/move", new
        {
            status = "REVIEW",
            sortOrder = 0,
            version = lecturerTask.Version
        }, Json);
        Assert.Equal(HttpStatusCode.OK, lecturerMoveRes.StatusCode);

        // --- 3. ADMIN ROLE TEST ---
        // Admin can access board
        var adminBoardRes = await admin.GetAsync($"/api/v1/projects/{projectId}/board");
        Assert.Equal(HttpStatusCode.OK, adminBoardRes.StatusCode);

        // Admin can create task
        var adminCreateRes = await admin.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Task created by System Administrator",
            priority = "LOW"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, adminCreateRes.StatusCode);
        var adminTask = (await adminCreateRes.Content.ReadFromJsonAsync<Envelope<TaskDto>>(Json))!.Data;

        // Admin can move task
        var adminMoveRes = await admin.PostAsJsonAsync($"/api/v1/tasks/{adminTask.Id}/move", new
        {
            status = "DONE",
            sortOrder = 0,
            version = adminTask.Version
        }, Json);
        Assert.Equal(HttpStatusCode.OK, adminMoveRes.StatusCode);

        // --- 4. UNAUTHORIZED OUTSIDER STUDENT TEST ---
        var outsider = await RegisterStudentAsync();
        var outsiderBoardRes = await outsider.GetAsync($"/api/v1/projects/{projectId}/board");
        Assert.Equal(HttpStatusCode.NotFound, outsiderBoardRes.StatusCode);

        var outsiderCreateRes = await outsider.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Malicious task injection attempt",
            priority = "HIGH"
        }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderCreateRes.StatusCode);

        var outsiderMoveRes = await outsider.PostAsJsonAsync($"/api/v1/tasks/{companyTask.Id}/move", new
        {
            status = "DONE",
            sortOrder = 0,
            version = companyTask.Version
        }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderMoveRes.StatusCode);
    }

    #region Helpers

    private async Task<(HttpClient Company, HttpClient Admin)> SeedAsync()
    {
        var admin = await CreateAdminClientAsync();
        var company = await CreateVerifiedCompanyAsync();
        return (company, admin);
    }

    private async Task<HttpClient> CreateVerifiedCompanyAsync()
    {
        var email = $"company-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Kiểm thử Kanban",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var companyName = $"Công ty {Guid.NewGuid():N}";
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = companyName }, Json);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var comp = await dbContext.Companies.SingleAsync(item => item.Name == companyName);
            comp.MarkVerified(DateTimeOffset.UtcNow, null);
            await dbContext.SaveChangesAsync();
        });

        return client;
    }

    private async Task<HttpClient> RegisterStudentAsync()
    {
        var email = $"student-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Sinh viên Kiểm thử Kanban",
            studentCode = "22" + Random.Shared.Next(100000, 999999),
            password = Password,
            accountType = "Student"
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
            var user = new User($"admin-{Guid.NewGuid():N}@skillbridge.local", "Quản trị viên Kanban", "seed-hash");
            var adminRole = await dbContext.Roles.SingleAsync(r => r.NormalizedName == RoleNames.Admin);
            user.AssignRole(adminRole.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            adminUserId = user.Id;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateRoleToken(RoleNames.Admin, adminUserId));
        return client;
    }

    private async Task<(HttpClient Client, Guid LecturerProfileId)> CreateLecturerClientAsync()
    {
        Guid lecturerUserId = default;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new User($"lecturer-{Guid.NewGuid():N}@skillbridge.local", "Giảng viên Kanban", "seed-hash");
            var role = await dbContext.Roles.SingleAsync(item => item.NormalizedName == RoleNames.Lecturer);
            user.AssignRole(role.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            lecturerUserId = user.Id;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateRoleToken(RoleNames.Lecturer, lecturerUserId));
        var profile = (await client.GetFromJsonAsync<Envelope<LecturerProfileDto>>("/api/v1/lecturers/me", Json))!.Data;
        return (client, profile.Id);
    }

    private static string CreateRoleToken(string role, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        foreach (var permission in IdentitySeed.GetPermissionsForRole(role))
        {
            claims.Add(new Claim("permission", permission));
        }
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<Guid> PublishProjectAsync(HttpClient company, HttpClient admin, string title)
    {
        var skillIds = await GetActiveSkillIdsAsync(1);
        var draft = await company.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title,
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            expectedStudentCount = 3,
            minTeamSize = 1,
            maxTeamSize = 4,
            skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var project = (await draft.Content.ReadFromJsonAsync<Envelope<ProjectDto>>(Json))!.Data;

        var updated = await company.PutAsJsonAsync($"/api/v1/company/projects/{project.Id}", new
        {
            title,
            summary = $"Tóm tắt {title}",
            problemStatement = "Vấn đề thực tế cần giải quyết.",
            businessRequirements = "Yêu cầu nghiệp vụ rõ ràng.",
            technicalConstraints = "Ràng buộc kỹ thuật .NET.",
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            applicationDeadline = DateTimeOffset.UtcNow.AddDays(14),
            expectedStudentCount = 3,
            minTeamSize = 1,
            maxTeamSize = 4,
            allowanceAmount = 2_000_000,
            allowanceCurrency = "VND",
            skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await company.PostAsync($"/api/v1/company/projects/{project.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{project.Id}/approve", new { }, Json)).StatusCode);

        return project.Id;
    }

    private async Task<List<Guid>> GetActiveSkillIdsAsync(int count)
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<ListEnvelope<CatalogSkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        return skills.Take(count).Select(skill => skill.Id).ToList();
    }

    private async Task AcceptAndConfirmAsync(HttpClient company, HttpClient student, Guid projectId)
    {
        var apply = await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json);
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var app = (await apply.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        var accept = await company.PostAsJsonAsync($"/api/v1/company/applications/{app.Id}/accept", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var confirm = await student.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    private sealed record Envelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);
    private sealed record AuthDto(string AccessToken);
    private sealed record ProjectDto(Guid Id, string Title);
    private sealed record CatalogSkillDto(Guid Id);
    private sealed record ApplicationDto(Guid Id, Guid ProjectId);
    private sealed record TaskDto(Guid Id, Guid ProjectId, string Title, string Status, Guid Version);
    private sealed record LecturerProfileDto(Guid Id);
    private sealed record AssignmentDto(Guid Id, Guid ProjectId, string Status);

    #endregion
}
