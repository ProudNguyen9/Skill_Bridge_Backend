using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class LecturerEndpointTests(CatalogApiFactory factory)
{
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";

    [Fact]
    public async Task Get_Me_LazilyCreatesActiveProfile()
    {
        var (client, _) = await CreateLecturerClientAsync();

        var response = await client.GetAsync("/api/v1/lecturers/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Envelope<LecturerProfileDto>>(Json);
        Assert.NotNull(payload);
        Assert.True(payload!.Data.IsActive);
        Assert.False(payload.Data.IsProfilePublic);
        Assert.Null(payload.Data.LecturerCode);
    }

    [Fact]
    public async Task Put_Me_UpdatesProfileFields()
    {
        var (client, _) = await CreateLecturerClientAsync();

        var updated = await client.PutAsJsonAsync("/api/v1/lecturers/me", new
        {
            lecturerCode = "GV001",
            department = "Khoa Công nghệ thông tin",
            academicTitle = "Giảng viên chính",
            bio = "Giảng viên DNTU.",
            websiteUrl = "https://dntu.edu.vn/~gv001",
            officeLocation = "Phòng A1.05",
            phoneNumber = "0908000111",
            isProfilePublic = true,
            showContactInfo = false
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var payload = await updated.Content.ReadFromJsonAsync<Envelope<LecturerProfileDto>>(Json);
        Assert.NotNull(payload);
        Assert.Equal("GV001", payload!.Data.LecturerCode);
        Assert.Equal("Khoa Công nghệ thông tin", payload.Data.Department);
        Assert.Equal("Giảng viên chính", payload.Data.AcademicTitle);
        Assert.Equal("Giảng viên DNTU.", payload.Data.Bio);
        Assert.Equal("Phòng A1.05", payload.Data.OfficeLocation);
        Assert.True(payload.Data.IsProfilePublic);
        Assert.False(payload.Data.ShowContactInfo);
        Assert.True(payload.Data.IsActive);
    }

    [Fact]
    public async Task Get_My_Assignments_ReturnsEmptyList_BeforeAnySupervision()
    {
        var (client, _) = await CreateLecturerClientAsync();

        var payload = await client.GetFromJsonAsync<ListEnvelope<LecturerAssignmentDto>>("/api/v1/lecturers/me/assignments", Json);
        Assert.NotNull(payload);
        Assert.Empty(payload!.Data);
    }

    [Fact]
    public async Task Admin_CanAssign_LecturerAccepts_ThenAdminRemoves()
    {
        var (lecturerClient, lecturerId) = await CreateLecturerClientAsync();
        var admin = CreateAdminClient();
        var projectId = Guid.NewGuid();

        var created = await admin.PostAsJsonAsync($"/api/v1/lecturers/{lecturerId}/assignments", new
        {
            projectId,
            role = "PRIMARY",
            note = "Phân công giảng viên hướng dẫn"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var assignment = (await created.Content.ReadFromJsonAsync<Envelope<LecturerAssignmentDto>>(Json))!.Data;
        Assert.Equal("INVITED", assignment.Status);
        Assert.Equal("PRIMARY", assignment.Role);
        Assert.Equal(projectId, assignment.ProjectId);

        var mine = (await lecturerClient.GetFromJsonAsync<ListEnvelope<LecturerAssignmentDto>>("/api/v1/lecturers/me/assignments", Json))!.Data;
        Assert.Single(mine);
        Assert.Equal("INVITED", mine.First().Status);

        var accepted = await lecturerClient.PutAsync($"/api/v1/lecturers/me/assignments/{assignment.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var acceptedPayload = (await accepted.Content.ReadFromJsonAsync<Envelope<LecturerAssignmentDto>>(Json))!.Data;
        Assert.Equal("ACTIVE", acceptedPayload.Status);
        Assert.NotNull(acceptedPayload.AcceptedAt);

        // Re-accepting an already ACTIVE assignment must conflict.
        var reAccept = await lecturerClient.PutAsync($"/api/v1/lecturers/me/assignments/{assignment.Id}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, reAccept.StatusCode);

        var removed = await admin.DeleteAsync($"/api/v1/lecturers/assignments/{assignment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var after = (await lecturerClient.GetFromJsonAsync<ListEnvelope<LecturerAssignmentDto>>("/api/v1/lecturers/me/assignments", Json))!.Data;
        Assert.Empty(after);

        var foreignDelete = await admin.DeleteAsync($"/api/v1/lecturers/assignments/{assignment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);
    }

    [Fact]
    public async Task Admin_Assignment_RejectsSecondAssignmentForSameProject()
    {
        var (_, firstLecturerId) = await CreateLecturerClientAsync();
        var (_, secondLecturerId) = await CreateLecturerClientAsync();
        var admin = CreateAdminClient();
        var projectId = Guid.NewGuid();

        var first = await admin.PostAsJsonAsync($"/api/v1/lecturers/{firstLecturerId}/assignments", new
        {
            projectId,
            role = "SUPERVISOR"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsJsonAsync($"/api/v1/lecturers/{secondLecturerId}/assignments", new
        {
            projectId,
            role = "SUPERVISOR"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Admin_Assignment_RejectsInactiveLecturer_AndUnknownLecturer()
    {
        var (_, lecturerId) = await CreateLecturerClientAsync();
        var admin = CreateAdminClient();

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.LecturerProfiles.AsTracking().SingleAsync(item => item.Id == lecturerId);
            profile.Suspend();
            await dbContext.SaveChangesAsync();
        });

        var inactive = await admin.PostAsJsonAsync($"/api/v1/lecturers/{lecturerId}/assignments", new
        {
            projectId = Guid.NewGuid(),
            role = "SUPERVISOR"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, inactive.StatusCode);

        var unknown = await admin.PostAsJsonAsync($"/api/v1/lecturers/{Guid.NewGuid()}/assignments", new
        {
            projectId = Guid.NewGuid(),
            role = "SUPERVISOR"
        }, Json);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Duplicate_LecturerCode_IsRejected_ByUniqueIndex()
    {
        var (_, firstId) = await CreateLecturerClientAsync();
        var (_, secondId) = await CreateLecturerClientAsync();

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var first = await dbContext.LecturerProfiles.AsTracking().SingleAsync(item => item.Id == firstId);
            first.Update("GV-DUP", null, null, null, null, null, null, false, false);
            await dbContext.SaveChangesAsync();

            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var second = await dbContext.LecturerProfiles.AsTracking().SingleAsync(item => item.Id == secondId);
            second.Update("GV-DUP", null, null, null, null, null, null, false, false);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Auth_Me_ReportsLecturerProfileType()
    {
        var (client, _) = await CreateLecturerClientAsync();

        var me = await client.GetFromJsonAsync<Envelope<CurrentUserDto>>("/api/v1/auth/me", Json);
        Assert.NotNull(me);
        Assert.Equal("LECTURER", me!.Data.ProfileType);
        Assert.NotEqual(Guid.Empty, me.Data.LecturerId!.Value);
    }

    [Fact]
    public async Task Non_Lecturer_Role_IsForbidden_FromLecturerEndpoints()
    {
        var email = $"student-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Sinh viên Kiểm thử",
            password = Password,
            accountType = "Student"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/v1/lecturers/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var assignments = await client.GetAsync("/api/v1/lecturers/me/assignments");
        Assert.Equal(HttpStatusCode.Forbidden, assignments.StatusCode);
    }

    [Fact]
    public async Task Student_And_Company_CannotCreateOrReadTechnicalReviews()
    {
        var student = await CreateAuthenticatedAccountAsync("Student");
        var company = await CreateAuthenticatedAccountAsync("Company");
        var submissionId = Guid.NewGuid();
        var request = new
        {
            version = Guid.NewGuid(),
            decision = "APPROVED",
            feedback = "Không được phép tạo đánh giá kỹ thuật.",
            criteriaNotes = "{}"
        };

        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync($"/api/v1/lecturer/submissions/{submissionId}/technical-review", request, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await company.PostAsJsonAsync($"/api/v1/lecturer/submissions/{submissionId}/technical-review", request, Json)).StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedAccountAsync(string accountType)
    {
        var client = factory.CreateClient();
        var email = $"technical-review-{accountType}-{Guid.NewGuid():N}@skillbridge.local";
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Tài khoản phân quyền đánh giá",
            password = Password,
            accountType
        }, Json);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Creates a LECTURER user directly (registration only supports student/company), then lazily provisions the profile via GET /me.</summary>
    private async Task<(HttpClient Client, Guid LecturerProfileId)> CreateLecturerClientAsync()
    {
        var userId = Guid.Empty;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new Domain.Identity.User($"lecturer-{Guid.NewGuid():N}@skillbridge.local", "Giảng viên Kiểm thử", "not-a-real-hash");
            var role = await dbContext.Roles.SingleAsync(role => role.NormalizedName == RoleNames.Lecturer);
            user.AssignRole(role.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            userId = user.Id;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(RoleNames.Lecturer, userId));

        var profile = (await client.GetFromJsonAsync<Envelope<LecturerProfileDto>>("/api/v1/lecturers/me", Json))!.Data;
        return (client, profile.Id);
    }

    private HttpClient CreateAdminClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(RoleNames.Admin, Guid.NewGuid()));
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

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);

    private sealed record AuthDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId);

    private sealed record CurrentUserDto(Guid UserId, string Email, string DisplayName, bool EmailVerified, bool IsActive, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions, string? ProfileType, Guid? StudentId, Guid? CompanyId, Guid? LecturerId, bool ProfileCompleted);

    private sealed record LecturerProfileDto(Guid Id, string? LecturerCode, string? Department, string? AcademicTitle, string? Bio, string? WebsiteUrl, string? OfficeLocation, string? PhoneNumber, bool IsProfilePublic, bool ShowContactInfo, bool IsActive);

    private sealed record LecturerAssignmentDto(Guid Id, Guid LecturerId, Guid ProjectId, string Role, string Status, DateTimeOffset AssignedAt, DateTimeOffset? AcceptedAt, DateTimeOffset? EndedAt, string? Note);
}
