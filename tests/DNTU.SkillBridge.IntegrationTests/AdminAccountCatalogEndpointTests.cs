using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// Kiểm thử hồi quy các chức năng quản trị bổ sung: khóa/mở/k cập nhật vai trò tài khoản (#5),
/// quản trị danh mục ghi được (#9), chi tiết hồ sơ giảng viên (#2), suspend/resume công ty (#3)
/// và broadcast thông báo quản trị (#13).
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class AdminAccountCatalogEndpointTests(CatalogApiFactory factory)
{
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private const string Password = "AuthFixture#2026";
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;

    // ─────────────────────────────── #5 Quản lý tài khoản ───────────────────────────────

    [Fact]
    public async Task Unauthenticated_UserManagement_Returns401()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/deactivate", new { }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Student)]
    [InlineData(RoleNames.Lecturer)]
    [InlineData(RoleNames.Company)]
    public async Task NonAdmin_UserManagement_Returns403_EvenWithAllPermissionClaims(string role)
    {
        using var client = CreateJwtClient(role, withAllPermissions: true);
        using var deactivate = await client.PostAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/deactivate", new { }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        using var roles = await client.PutAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/roles",
            new { roles = new[] { RoleNames.Student } }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, roles.StatusCode);
    }

    [Fact]
    public async Task Deactivate_UnknownUser_Returns404()
    {
        using var client = await CreateAdminClientAsync();
        using var response = await client.PostAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/deactivate", new { }, Json);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_ThenLogin_IsBlocked_AndActivate_Restores()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var email = $"victim-{stamp}@skillbridge.local";
        var userId = await SeedUserAsync($"{email}", RoleNames.Student, password: Password);
        var admin = await CreateAdminClientAsync();

        using var deactivate = await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/deactivate", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var payload = JsonDocument.Parse(await deactivate.Content.ReadAsStringAsync());
        Assert.False(payload.RootElement.GetProperty("data").GetProperty("isActive").GetBoolean());

        using var blockedLogin = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.True((int)blockedLogin.StatusCode >= 400, $"login should be blocked, got {blockedLogin.StatusCode}");

        using var activate = await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/activate", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        using var restoredLogin = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, restoredLogin.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeactivateSelf_Returns409()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var email = $"selfadmin-{stamp}@skillbridge.local";
        // JWT sub = chính user này → nút tự khóa bị chặn
        var userId = await SeedUserAsync(email, RoleNames.Admin);
        using var client = CreateJwtClientWithSubject(RoleNames.Admin, userId);
        using var response = await client.PostAsJsonAsync($"/api/v1/admin/users/{userId}/deactivate", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeactivateSuperAdmin_Returns409()
    {
        var superId = await SeedUserAsync($"super-target-{Guid.NewGuid():N}@skillbridge.local", RoleNames.SuperAdmin);
        using var admin = await CreateAdminClientAsync(RoleNames.Admin);
        using var response = await admin.PostAsJsonAsync($"/api/v1/admin/users/{superId}/deactivate", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanDeactivateAdmin_AndRestore()
    {
        var adminId = await SeedUserAsync($"admin-target-{Guid.NewGuid():N}@skillbridge.local", RoleNames.Admin);
        using var super = await CreateAdminClientAsync(RoleNames.SuperAdmin);
        using var deactivate = await super.PostAsJsonAsync($"/api/v1/admin/users/{adminId}/deactivate", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        using var activate = await super.PostAsJsonAsync($"/api/v1/admin/users/{adminId}/activate", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotGrantAdminTierRoles_Returns409()
    {
        var target = await SeedUserAsync($"role-victim-{Guid.NewGuid():N}@skillbridge.local", RoleNames.Student);
        using var admin = await CreateAdminClientAsync(RoleNames.Admin);
        foreach (var elevated in new[] { RoleNames.Admin, RoleNames.SuperAdmin })
        {
            using var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{target}/roles",
                new { roles = new[] { RoleNames.Student, elevated } }, Json);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    [Fact]
    public async Task SuperAdmin_CanReplaceRoles_AndUnknownRole_Returns409()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var target = await SeedUserAsync($"role-ok-{stamp}@skillbridge.local", RoleNames.Student);
        using var super = await CreateAdminClientAsync(RoleNames.SuperAdmin);

        using var unknown = await super.PutAsJsonAsync($"/api/v1/admin/users/{target}/roles",
            new { roles = new[] { "WIZARD" } }, Json);
        Assert.Equal(HttpStatusCode.Conflict, unknown.StatusCode);

        using var empty = await super.PutAsJsonAsync($"/api/v1/admin/users/{target}/roles",
            new { roles = Array.Empty<string>() }, Json);
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);

        using var update = await super.PutAsJsonAsync($"/api/v1/admin/users/{target}/roles",
            new { roles = new[] { RoleNames.Lecturer, RoleNames.Company } }, Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var roles = JsonDocument.Parse(await update.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()).ToHashSet();
        Assert.Equal(new HashSet<string?> { RoleNames.Lecturer, RoleNames.Company }, roles);
    }

    // ─────────────────────────────── #2 Chi tiết giảng viên ───────────────────────────────

    [Fact]
    public async Task LecturerDetail_AfterReject_ExposesReviewNote()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var email = $"lect-detail-{stamp}@skillbridge.local";
        var lecturerId = await RegisterLecturerAsync(email, stamp);
        var admin = await CreateAdminClientAsync();

        using var reject = await admin.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{lecturerId}/reject", new { note = "Thiếu mã giảng viên hợp lệ" }, Json);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        using var detail = await admin.GetAsync($"/api/v1/admin/lecturers/{lecturerId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var data = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal("REJECTED", data.GetProperty("approvalStatus").GetString());
        Assert.Equal("Thiếu mã giảng viên hợp lệ", data.GetProperty("reviewNote").GetString());
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.NotNull(data.GetProperty("reviewedAt").GetString());

        using var missing = await admin.GetAsync($"/api/v1/admin/lecturers/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ─────────────────────────────── #3 Suspend / resume công ty ───────────────────────────────

    [Fact]
    public async Task Company_SuspendRequiresNote_ThenResume_RestoresVerified()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var companyId = await OnboardAndVerifyCompanyAsync(stamp);
        var admin = await CreateAdminClientAsync();

        using var noNote = await admin.PostAsJsonAsync($"/api/v1/admin/companies/{companyId}/suspend", new { }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);

        using var suspend = await admin.PostAsJsonAsync(
            $"/api/v1/admin/companies/{companyId}/suspend", new { note = "Vi phạm chính sách đăng tin" }, Json);
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        using var again = await admin.PostAsJsonAsync(
            $"/api/v1/admin/companies/{companyId}/suspend", new { note = "đã suspend rồi" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        using var resume = await admin.PostAsJsonAsync($"/api/v1/admin/companies/{companyId}/resume", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        var status = JsonDocument.Parse(await resume.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetString();
        Assert.Equal("VERIFIED", status);

        using var resumeAgain = await admin.PostAsJsonAsync($"/api/v1/admin/companies/{companyId}/resume", new { }, Json);
        Assert.Equal(HttpStatusCode.Conflict, resumeAgain.StatusCode);
    }

    // ─────────────────────────────── #9 Quản trị danh mục ───────────────────────────────

    [Fact]
    public async Task Skill_CreateUpdateDeactivate_FullLifecycle()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var code = $"SK{Random.Shared.Next(100000, 999999)}";
        var admin = await CreateAdminClientAsync();

        using var create = await admin.PostAsJsonAsync("/api/v1/admin/catalog/skills",
            new { code, name = $"Kỹ năng {stamp}", category = "TECHNICAL", description = "Tạo từ test hồi quy" }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var skillId = created.GetProperty("id").GetString()!;
        Assert.Equal("ACTIVE", created.GetProperty("isActive").GetBoolean() ? "ACTIVE" : "INACTIVE");

        using var duplicate = await admin.PostAsJsonAsync("/api/v1/admin/catalog/skills",
            new { code, name = $"Kỹ năng {stamp}", category = "TECHNICAL", description = "" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var badCategory = await admin.PostAsJsonAsync("/api/v1/admin/catalog/skills",
            new { code = $"{code}B", name = "Sai category", category = "NOT_A_CATEGORY", description = "" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, badCategory.StatusCode);

        using var update = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/skills/{skillId}",
            new { name = $"Kỹ năng {stamp} v2", category = "BUSINESS", description = "Đã cập nhật" }, Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = JsonDocument.Parse(await update.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal("BUSINESS", updated.GetProperty("category").GetString());

        using var deactivate = await admin.DeleteAsync($"/api/v1/admin/catalog/skills/{skillId}");
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        // Danh sách public ẩn kỹ năng inactive; admin includeInactive=true vẫn thấy
        using var publicList = await factory.CreateClient().GetAsync("/api/v1/catalog/skills");
        var publicBody = await publicList.Content.ReadAsStringAsync();
        Assert.DoesNotContain(skillId, publicBody);
        using var adminList = await admin.GetAsync("/api/v1/catalog/skills?includeInactive=true");
        Assert.Contains(skillId, await adminList.Content.ReadAsStringAsync());

        using var activate = await admin.PostAsJsonAsync($"/api/v1/admin/catalog/skills/{skillId}/activate", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Student)]
    [InlineData(RoleNames.Company)]
    [InlineData(RoleNames.Lecturer)]
    public async Task Catalog_Writes_AreForbiddenForNonAdmins(string role)
    {
        using var client = CreateJwtClient(role, withAllPermissions: true);
        using var create = await client.PostAsJsonAsync("/api/v1/admin/catalog/skills",
            new { code = "HACK1", name = "Không được phép", category = "TECHNICAL", description = "" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var delete = await client.DeleteAsync($"/api/v1/admin/catalog/skills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Faculty_Major_Create_AndRejectUnknownFaculty()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var admin = await CreateAdminClientAsync();
        var facultyCode = $"FK{Random.Shared.Next(100000, 999999)}";

        using var createFaculty = await admin.PostAsJsonAsync("/api/v1/admin/catalog/faculties",
            new { code = facultyCode, name = $"Khoa {stamp}" }, Json);
        Assert.Equal(HttpStatusCode.Created, createFaculty.StatusCode);
        var facultyId = JsonDocument.Parse(await createFaculty.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetString()!;

        using var majorMissingFaculty = await admin.PostAsJsonAsync("/api/v1/admin/catalog/majors",
            new { code = $"MJ{Random.Shared.Next(100000, 999999)}", name = "Ngành không có khoa", facultyId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, majorMissingFaculty.StatusCode);

        var majorCode = $"MJ{Random.Shared.Next(100000, 999999)}";
        using var createMajor = await admin.PostAsJsonAsync("/api/v1/admin/catalog/majors",
            new { code = majorCode, name = $"Ngành {stamp}", facultyId }, Json);
        Assert.Equal(HttpStatusCode.Created, createMajor.StatusCode);
        var major = JsonDocument.Parse(await createMajor.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal($"Khoa {stamp}", major.GetProperty("facultyName").GetString());

        using var duplicateMajor = await admin.PostAsJsonAsync("/api/v1/admin/catalog/majors",
            new { code = majorCode, name = "Trùng mã", facultyId }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicateMajor.StatusCode);
    }

    // ─────────────────────────────── #13 Broadcast ───────────────────────────────

    [Fact]
    public async Task Broadcast_ToRole_CreatesNotifications_AndValidatesInput()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        await SeedUserAsync($"broadcast-a-{stamp}@skillbridge.local", RoleNames.Student);
        await SeedUserAsync($"broadcast-b-{stamp}@skillbridge.local", RoleNames.Lecturer);
        var admin = await CreateAdminClientAsync();

        using var badTitle = await admin.PostAsJsonAsync("/api/v1/admin/notifications/broadcast",
            new { title = "   ", type = "ADMIN_BROADCAST", targetRoles = Array.Empty<string>() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, badTitle.StatusCode);

        using var broadcast = await admin.PostAsJsonAsync("/api/v1/admin/notifications/broadcast",
            new { title = $"Bảo trì hệ thống {stamp}", type = "ADMIN_BROADCAST", targetRoles = new[] { RoleNames.Student } }, Json);
        Assert.Equal(HttpStatusCode.OK, broadcast.StatusCode);
        var recipients = JsonDocument.Parse(await broadcast.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("recipients").GetInt32();
        Assert.True(recipients >= 1);
    }

    // ─────────────────────────────── Helpers ───────────────────────────────

    private async Task<Guid> SeedUserAsync(string email, string roleName, string? password = null)
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var role = await dbContext.Roles.SingleAsync(item => item.NormalizedName == roleName);
            var user = new User(email, $"Fixture {roleName}", string.Empty);
            if (password is not null)
            {
                user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, password));
            }
            user.SetEmailVerified();
            user.AssignRole(role.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        });
        await using var db = factory.CreateDbContext();
        return await db.Users.Where(item => item.Email == email).Select(item => item.Id).SingleAsync();
    }

    private async Task<string> RegisterLecturerAsync(string email, string stamp)
    {
        using var register = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new
            {
                email,
                displayName = $"Giảng viên {stamp}",
                password = Password,
                accountType = "Lecturer",
                lecturerCode = $"GV{Random.Shared.Next(100000, 999999)}"
            }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        await using var db = factory.CreateDbContext();
        var userId = await db.Users.Where(item => item.Email == email).Select(item => item.Id).SingleAsync();
        var lecturerId = await db.LecturerProfiles.Where(item => item.UserId == userId).Select(item => item.Id).SingleAsync();
        return lecturerId.ToString();
    }

    private async Task<string> OnboardAndVerifyCompanyAsync(string stamp)
    {
        var email = $"suspend-company-{stamp}@skillbridge.local";
        using var register = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, displayName = $"Công ty {stamp}", password = Password, accountType = "Company" }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Json);
        var accessToken = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        using var companyClient = factory.CreateClient();
        companyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var onboarding = await companyClient.PutAsJsonAsync("/api/v1/companies/me",
            new { name = $"Công ty TNHH Suspend {stamp}", taxCode = $"03{Random.Shared.Next(100000000, 999999999)}" }, Json);
        Assert.Equal(HttpStatusCode.OK, onboarding.StatusCode);

        await using var db = factory.CreateDbContext();
        var companyId = await db.Companies.Where(item => item.Name.Contains(stamp)).Select(item => item.Id).SingleAsync();

        using var admin = await CreateAdminClientAsync();
        using var verify = await admin.PostAsJsonAsync($"/api/v1/admin/companies/{companyId}/verify", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return companyId.ToString();
    }

    private async Task<HttpClient> CreateAdminClientAsync(string roleName = RoleNames.SuperAdmin)
    {
        var email = $"jwt-admin-{Guid.NewGuid():N}@skillbridge.local";
        var userId = await SeedUserAsync(email, roleName);
        return CreateJwtClientWithSubject(roleName, userId);
    }

    private HttpClient CreateJwtClient(string? role, bool withAllPermissions = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (withAllPermissions) claims.AddRange(PermissionNames.All.Select(permission => new Claim("permission", permission)));
        return BuildClient(claims);
    }

    private HttpClient CreateJwtClientWithSubject(string role, Guid subjectId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subjectId.ToString()),
            new(ClaimTypes.Role, role)
        };
        return BuildClient(claims);
    }

    private HttpClient BuildClient(IReadOnlyCollection<Claim> claims)
    {
        var token = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
