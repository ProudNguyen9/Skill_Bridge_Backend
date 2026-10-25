using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// Luồng thực tế: giảng viên tự đăng ký → chờ duyệt → admin duyệt; công ty đăng ký →
/// tạo hồ sơ (onboarding) → admin xác minh → được đăng bài toán.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class LecturerCompanyApprovalFlowTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "AuthFixture#2026";

    [Fact]
    public async Task Lecturer_SelfRegisters_ThenAdminApproves_UnlocksPortal()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var email = $"lect-flow-{stamp}@skillbridge.local";
        var lecturerCode = $"GV{Random.Shared.Next(100000, 999999)}";

        // 1. Giảng viên tự đăng ký
        using var client = factory.CreateClient();
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                email,
                displayName = $"Giảng viên {stamp}",
                password = Password,
                accountType = "Lecturer",
                lecturerCode
            },
            Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        // 2. Login và kiểm tra trạng thái PENDING
        using (var meDocument = await LoginAndGetCurrentUserAsync(email))
        {
            var me = meDocument.RootElement.GetProperty("data");
            Assert.Contains(RoleNames.Lecturer, me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
            Assert.Equal("PENDING", me.GetProperty("lecturerApprovalStatus").GetString());
        }

        // 3. Admin duyệt
        var admin = await CreateAuthenticatedClientAsync($"admin-flow-{stamp}@skillbridge.local", RoleNames.Admin);
        using var listResponse = await admin.GetAsync("/api/v1/admin/lecturers?status=pending");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var lecturerId = listDocument.RootElement.GetProperty("data").EnumerateArray()
            .First(item => item.GetProperty("email").GetString() == email)
            .GetProperty("lecturerId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(lecturerId));

        using var approveResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{lecturerId}/approve", new { note = "Đã xác minh với khoa" }, Json);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // 4. Giảng viên được duyệt
        using (var meAfterDocument = await LoginAndGetCurrentUserAsync(email))
        {
            Assert.Equal("APPROVED", meAfterDocument.RootElement.GetProperty("data").GetProperty("lecturerApprovalStatus").GetString());
        }
    }

    [Fact]
    public async Task Company_Onboards_ThenAdminVerifies_UnlocksPublishing()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var email = $"company-flow-{stamp}@skillbridge.local";

        // 1. Đăng ký tài khoản công ty
        using var client = factory.CreateClient();
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, displayName = $"Công ty {stamp}", password = Password, accountType = "Company" },
            Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        // 2. Login + onboarding: tạo hồ sơ công ty (PUT /companies/me)
        using var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginDocument = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginDocument.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var onboardingResponse = await client.PutAsJsonAsync(
            "/api/v1/companies/me",
            new
            {
                name = $"Công ty TNHH Test {stamp}",
                taxCode = $"03{Random.Shared.Next(100000000, 999999999)}",
                description = "Công ty đối tác phục vụ test duyệt"
            },
            Json);
        Assert.Equal(HttpStatusCode.OK, onboardingResponse.StatusCode);

        // 3. Admin xác minh công ty PENDING
        var admin = await CreateAuthenticatedClientAsync($"admin-company-{stamp}@skillbridge.local", RoleNames.Admin);
        using var listResponse = await admin.GetAsync("/api/v1/admin/companies?status=pending");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var companyId = listDocument.RootElement.GetProperty("data").EnumerateArray()
            .First(item => item.GetProperty("name").GetString()!.Contains(stamp))
            .GetProperty("companyId").GetString();

        using var verifyResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/companies/{companyId}/verify", new { note = "Đã đối chiếu giấy phép DN" }, Json);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        // 4. Công ty được mở quyền đăng bài toán
        using var meResponse = await client.GetAsync("/api/v1/companies/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var companyJson = await meResponse.Content.ReadAsStringAsync();
        using var meDocument = JsonDocument.Parse(companyJson);
        var company = meDocument.RootElement.GetProperty("data");
        Assert.Equal("VERIFIED", company.GetProperty("verificationStatus").GetString());
        Assert.True(company.GetProperty("canPublishProjects").GetBoolean());
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string roleName)
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

        using var loginResponse = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var document = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = document.RootElement.GetProperty("data").GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<JsonDocument> LoginAndGetCurrentUserAsync(string email)
    {
        using var loginResponse = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginDocument = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginDocument.RootElement.GetProperty("data").GetProperty("accessToken").GetString();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var meResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        return JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
    }
}
