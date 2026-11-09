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

    [Fact]
    public async Task CompanyStatusFilters_RejectInvalidStatus_AndUseDataEnvelope()
    {
        var firstStamp = Guid.NewGuid().ToString("N")[..8];
        var secondStamp = Guid.NewGuid().ToString("N")[..8];
        using var firstCompany = factory.CreateClient();
        using var secondCompany = factory.CreateClient();
        var firstName = await OnboardCompanyAsync(firstCompany, firstStamp);
        var secondName = await OnboardCompanyAsync(secondCompany, secondStamp);
        var admin = await CreateAuthenticatedClientAsync($"admin-company-filter-{firstStamp}@skillbridge.local", RoleNames.Admin);

        using var pendingResponse = await admin.GetAsync("/api/v1/admin/companies?status=pending");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        using var pendingDocument = JsonDocument.Parse(await pendingResponse.Content.ReadAsStringAsync());
        var pendingData = pendingDocument.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, pendingData.ValueKind);
        Assert.Contains(pendingData.EnumerateArray(), item => item.GetProperty("name").GetString() == firstName);
        Assert.Contains(pendingData.EnumerateArray(), item => item.GetProperty("name").GetString() == secondName);

        var firstId = pendingData.EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == firstName)
            .GetProperty("companyId").GetGuid();
        var secondId = pendingData.EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == secondName)
            .GetProperty("companyId").GetGuid();

        using var verifyResponse = await admin.PostAsJsonAsync($"/api/v1/admin/companies/{firstId}/verify", new { note = "verified" }, Json);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        using var verifiedDocument = JsonDocument.Parse(await verifyResponse.Content.ReadAsStringAsync());
        Assert.Equal("VERIFIED", verifiedDocument.RootElement.GetProperty("data").GetString());

        using var rejectResponse = await admin.PostAsJsonAsync($"/api/v1/admin/companies/{secondId}/reject", new { note = "rejected" }, Json);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        using var verifiedListResponse = await admin.GetAsync("/api/v1/admin/companies?status= verified ");
        Assert.Equal(HttpStatusCode.OK, verifiedListResponse.StatusCode);
        using var verifiedListDocument = JsonDocument.Parse(await verifiedListResponse.Content.ReadAsStringAsync());
        Assert.Contains(verifiedListDocument.RootElement.GetProperty("data").EnumerateArray(), item => item.GetProperty("name").GetString() == firstName);
        Assert.DoesNotContain(verifiedListDocument.RootElement.GetProperty("data").EnumerateArray(), item => item.GetProperty("name").GetString() == secondName);

        using var rejectedListResponse = await admin.GetAsync("/api/v1/admin/companies?status=REJECTED");
        Assert.Equal(HttpStatusCode.OK, rejectedListResponse.StatusCode);
        using var rejectedListDocument = JsonDocument.Parse(await rejectedListResponse.Content.ReadAsStringAsync());
        Assert.Contains(rejectedListDocument.RootElement.GetProperty("data").EnumerateArray(), item => item.GetProperty("name").GetString() == secondName);

        using var invalidStatusResponse = await admin.GetAsync("/api/v1/admin/companies?status=unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatusResponse.StatusCode);
    }

    [Fact]
    public async Task CompanyReject_RequiresTrimmedNote_PersistsNote_AndIsPendingOnly()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        using var company = factory.CreateClient();
        var companyName = await OnboardCompanyAsync(company, stamp);
        var admin = await CreateAuthenticatedClientAsync($"admin-company-reject-{stamp}@skillbridge.local", RoleNames.Admin);

        using var pendingListResponse = await admin.GetAsync("/api/v1/admin/companies?status=PENDING");
        using var pendingListDocument = JsonDocument.Parse(await pendingListResponse.Content.ReadAsStringAsync());
        var companyId = pendingListDocument.RootElement.GetProperty("data").EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == companyName)
            .GetProperty("companyId").GetGuid();
        var endpoint = $"/api/v1/admin/companies/{companyId}/reject";

        foreach (var note in new object?[] { null, "   ", new string('x', 1001) })
        {
            using var invalidResponse = await admin.PostAsJsonAsync(endpoint, new { note }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }

        using var rejectResponse = await admin.PostAsJsonAsync(endpoint, new { note = "  Missing registration document  " }, Json);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        using var rejectDocument = JsonDocument.Parse(await rejectResponse.Content.ReadAsStringAsync());
        Assert.Equal("REJECTED", rejectDocument.RootElement.GetProperty("data").GetString());

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var persisted = await dbContext.Companies.SingleAsync(item => item.Id == companyId);
            Assert.Equal("Missing registration document", persisted.VerificationNote);
        });

        using var repeatedRejectResponse = await admin.PostAsJsonAsync(endpoint, new { note = "another reason" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, repeatedRejectResponse.StatusCode);
        using var crossDecisionResponse = await admin.PostAsJsonAsync($"/api/v1/admin/companies/{companyId}/verify", new { note = "should conflict" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, crossDecisionResponse.StatusCode);
    }

    [Fact]
    public async Task LecturerReject_RequiresTrimmedNote_PersistsRejection_AndIsPendingOnly()
    {
        var email = await RegisterLecturerAsync();
        using var admin = await CreateAuthenticatedClientAsync($"admin-lecturer-reject-{Guid.NewGuid():N}@skillbridge.local", RoleNames.Admin);
        using var pendingResponse = await admin.GetAsync("/api/v1/admin/lecturers?status=PENDING");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        using var pendingDocument = JsonDocument.Parse(await pendingResponse.Content.ReadAsStringAsync());
        var lecturerId = pendingDocument.RootElement.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("email").GetString() == email)
            .GetProperty("lecturerId").GetGuid();
        var endpoint = $"/api/v1/admin/lecturers/{lecturerId}/reject";

        foreach (var note in new string?[] { null, "", "   ", new string('x', 1001) })
        {
            using var invalidResponse = await admin.PostAsJsonAsync(endpoint, new { note }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var persisted = await dbContext.LecturerProfiles.SingleAsync(item => item.Id == lecturerId);
            Assert.Equal("PENDING", persisted.ApprovalStatus.ToString());
            Assert.Null(persisted.ReviewNote);
        });

        using var rejectResponse = await admin.PostAsJsonAsync(endpoint, new { note = "  Missing faculty confirmation  " }, Json);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        using var rejectDocument = JsonDocument.Parse(await rejectResponse.Content.ReadAsStringAsync());
        Assert.Equal("REJECTED", rejectDocument.RootElement.GetProperty("data").GetString());

        using var repeatedRejectResponse = await admin.PostAsJsonAsync(endpoint, new { note = "another reason" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, repeatedRejectResponse.StatusCode);
        using var approveAfterRejectResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{lecturerId}/approve", new { note = "should conflict" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, approveAfterRejectResponse.StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var persisted = await dbContext.LecturerProfiles.SingleAsync(item => item.Id == lecturerId);
            Assert.Equal("REJECTED", persisted.ApprovalStatus.ToString());
            Assert.Equal("Missing faculty confirmation", persisted.ReviewNote);
        });
    }

    [Fact]
    public async Task LecturerStatusFilters_IsolateEachStatus_RejectInvalidStatus_AndApprovalIsPendingOnly()
    {
        var pendingEmail = await RegisterLecturerAsync();
        var approvedEmail = await RegisterLecturerAsync();
        var rejectedEmail = await RegisterLecturerAsync();
        using var admin = await CreateAuthenticatedClientAsync($"admin-lecturer-filter-{Guid.NewGuid():N}@skillbridge.local", RoleNames.Admin);

        using var pendingResponse = await admin.GetAsync("/api/v1/admin/lecturers?status=PENDING");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        using var pendingDocument = JsonDocument.Parse(await pendingResponse.Content.ReadAsStringAsync());
        var pendingData = pendingDocument.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, pendingData.ValueKind);
        var pendingId = pendingData.EnumerateArray().Single(item => item.GetProperty("email").GetString() == pendingEmail)
            .GetProperty("lecturerId").GetGuid();
        var approvedId = pendingData.EnumerateArray().Single(item => item.GetProperty("email").GetString() == approvedEmail)
            .GetProperty("lecturerId").GetGuid();
        var rejectedId = pendingData.EnumerateArray().Single(item => item.GetProperty("email").GetString() == rejectedEmail)
            .GetProperty("lecturerId").GetGuid();

        using var approveResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{approvedId}/approve", new { note = "Faculty confirmed" }, Json);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        using var approveDocument = JsonDocument.Parse(await approveResponse.Content.ReadAsStringAsync());
        Assert.Equal("APPROVED", approveDocument.RootElement.GetProperty("data").GetString());

        using var repeatedApproveResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{approvedId}/approve", new { note = "should conflict" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, repeatedApproveResponse.StatusCode);
        using var rejectAfterApproveResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{approvedId}/reject", new { note = "should conflict" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, rejectAfterApproveResponse.StatusCode);

        using var rejectResponse = await admin.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{rejectedId}/reject", new { note = "Faculty not confirmed" }, Json);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        foreach (var (status, expectedId) in new[]
        {
            ("PENDING", pendingId),
            (" approved ", approvedId),
            ("REJECTED", rejectedId)
        })
        {
            using var listResponse = await admin.GetAsync($"/api/v1/admin/lecturers?status={Uri.EscapeDataString(status)}");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
            var data = listDocument.RootElement.GetProperty("data");
            Assert.Equal(JsonValueKind.Array, data.ValueKind);
            var ids = data.EnumerateArray().Select(item => item.GetProperty("lecturerId").GetGuid()).ToArray();
            Assert.Contains(expectedId, ids);
            foreach (var otherId in new[] { pendingId, approvedId, rejectedId }.Where(id => id != expectedId))
            {
                Assert.DoesNotContain(otherId, ids);
            }
        }

        using var invalidStatusResponse = await admin.GetAsync("/api/v1/admin/lecturers?status=unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatusResponse.StatusCode);
    }

    private async Task<string> RegisterLecturerAsync()
    {
        var stamp = Guid.NewGuid().ToString("N");
        var email = $"lecturer-negative-{stamp}@skillbridge.local";
        using var client = factory.CreateClient();
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                email,
                displayName = $"Lecturer {stamp}",
                password = Password,
                accountType = "Lecturer",
                lecturerCode = $"GV{stamp[..30]}"
            },
            Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        return email;
    }

    private async Task<string> OnboardCompanyAsync(HttpClient client, string stamp)
    {
        var email = $"company-{stamp}@skillbridge.local";
        var name = $"Công ty TNHH Test {stamp}";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, displayName = $"Công ty {stamp}", password = Password, accountType = "Company" },
            Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        using var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        using var loginDocument = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginDocument.RootElement.GetProperty("data").GetProperty("accessToken").GetString());

        using var onboardingResponse = await client.PutAsJsonAsync(
            "/api/v1/companies/me",
            new { name, taxCode = $"03{Random.Shared.Next(100000000, 999999999)}", description = "Công ty test" },
            Json);
        Assert.Equal(HttpStatusCode.OK, onboardingResponse.StatusCode);
        return name;
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
