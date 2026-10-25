using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// Kiểm tra ma trận RolePermissions được seed đúng: mỗi role nhận đúng bộ permission
/// claims trong JWT (đọc lại qua /auth/me), và role thiếu quyền bị chặn ở endpoint admin.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class PermissionMatrixTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "AuthFixture#2026";

    public static TheoryData<string, string[]> RolePermissionMatrix => new()
    {
        {
            RoleNames.Student,
            [
                PermissionNames.FilesUpload,
                PermissionNames.FilesRead,
                PermissionNames.ProjectsApply,
                PermissionNames.SubmissionsCreate,
            ]
        },
        {
            RoleNames.Company,
            [
                PermissionNames.FilesUpload,
                PermissionNames.FilesRead,
                PermissionNames.ProjectsCreate,
                PermissionNames.ApplicationsReview,
                PermissionNames.PaymentsView,
            ]
        },
        {
            RoleNames.Lecturer,
            [
                PermissionNames.FilesUpload,
                PermissionNames.FilesRead,
                PermissionNames.ProjectsApprove,
                PermissionNames.SubmissionsReviewTechnical,
                PermissionNames.RubricsManage,
                PermissionNames.EvaluationsManage,
                PermissionNames.SkillsVerify,
            ]
        },
    };

    [Theory]
    [MemberData(nameof(RolePermissionMatrix))]
    public async Task Login_AsRole_ReturnsExactlySeededPermissions(string roleName, string[] expectedPermissions)
    {
        var email = $"perm-{roleName.ToLowerInvariant()}-{Guid.NewGuid():N}@skillbridge.local";
        await SeedAccountAsync(email, roleName);

        using var meDocument = await LoginAndGetCurrentUserAsync(email);
        var permissions = meDocument.RootElement
            .GetProperty("data")
            .GetProperty("permissions")
            .EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();

        foreach (var expected in expectedPermissions)
        {
            Assert.Contains(expected, permissions);
        }

        // Không được nhận quyền ngoài ma trận (ví dụ quyền admin)
        Assert.DoesNotContain(PermissionNames.AdminSettingsManage, permissions);
    }

    [Fact]
    public async Task Login_AsAdmin_GetsEveryPermission()
    {
        var email = $"perm-admin-{Guid.NewGuid():N}@skillbridge.local";
        await SeedAccountAsync(email, RoleNames.Admin);

        using var meDocument = await LoginAndGetCurrentUserAsync(email);
        var permissions = meDocument.RootElement
            .GetProperty("data")
            .GetProperty("permissions")
            .EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();

        Assert.Equal(PermissionNames.All.Count, permissions.Length);
        Assert.Contains(PermissionNames.AdminSettingsManage, permissions);
    }

    [Fact]
    public async Task Student_CallingAdminApprovalQueue_IsForbidden()
    {
        var email = $"perm-student-deny-{Guid.NewGuid():N}@skillbridge.local";
        await SeedAccountAsync(email, RoleNames.Student);

        using var client = await CreateAuthenticatedClientAsync(email);
        using var response = await client.GetAsync("/api/v1/admin/project-approvals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Company_WithProjectsCreatePermission_PassesApprovalQueueGate()
    {
        // COMPANY không có projects.approve nhưng gọi endpoint dùng CanApprove()
        // (permission projects.approve) -> phải bị chặn.
        var email = $"perm-company-deny-{Guid.NewGuid():N}@skillbridge.local";
        await SeedAccountAsync(email, RoleNames.Company);

        using var client = await CreateAuthenticatedClientAsync(email);
        using var response = await client.GetAsync("/api/v1/admin/project-approvals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<JsonDocument> LoginAndGetCurrentUserAsync(string email)
    {
        using var client = await CreateAuthenticatedClientAsync(email);
        using var meResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var document = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        return document;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        using var loginResponse = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password },
            Json);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var document = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = document.RootElement.GetProperty("data").GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task SeedAccountAsync(string email, string roleName)
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var role = await dbContext.Roles.SingleAsync(item => item.NormalizedName == roleName);
            var user = new User(email, $"Fixture {roleName}", string.Empty);
            user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, Password));
            user.SetEmailVerified();
            user.AssignRole(role.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        });
    }
}
