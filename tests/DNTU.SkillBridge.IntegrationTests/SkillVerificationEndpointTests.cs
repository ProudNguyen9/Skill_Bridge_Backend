using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// Kiểm thử tích hợp toàn trình luồng chứng chỉ/kỹ năng thực tế:
/// Admin tạo chứng chỉ mới -> Sinh viên chọn và gửi minh chứng -> Giảng viên yêu cầu bổ sung / từ chối / phê duyệt -> Cấp VerifiedSkill vào Skill Passport.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class SkillVerificationEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "AuthFixture#2026";

    [Fact]
    public async Task EndToEnd_AdminCreatesSkill_StudentSubmits_LecturerRequestsChangesThenApproves_GrantsVerifiedSkill()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var admin = await RegisterAdminAsync(stamp);
        var student = await RegisterStudentAsync(stamp);
        var lecturer = await RegisterApprovedLecturerAsync(admin, stamp);

        // 1. Admin tạo chứng chỉ mới trong danh mục chuẩn
        var skillCode = $"CERT_{stamp.ToUpperInvariant()}";
        var badgeUrl = $"/api/v1/files/view?key=uploads%2Fbadge_{stamp}.png";
        using var createSkill = await admin.PostAsJsonAsync("/api/v1/admin/catalog/skills", new
        {
            code = skillCode,
            name = $"Chứng chỉ Cloud Architecture {stamp}",
            category = "ENGINEERING",
            description = "Thiết kế hạ tầng đám mây và triển khai thực tế.",
            badgeImageUrl = badgeUrl,
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createSkill.StatusCode);
        var skillId = JsonDocument.Parse(await createSkill.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        // Active catalog trả về chứng chỉ mới
        using var activeCatalog = await student.GetAsync("/api/v1/catalog/skills");
        Assert.Equal(HttpStatusCode.OK, activeCatalog.StatusCode);
        var activeCatalogJson = JsonDocument.Parse(await activeCatalog.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Contains(activeCatalogJson.EnumerateArray(), s => s.GetProperty("id").GetGuid() == skillId);

        // 2. Sinh viên gửi hồ sơ rỗng minh chứng -> 400 BadRequest
        using var emptySubmit = await student.PostAsJsonAsync("/api/v1/students/skills/requests", new
        {
            skillId,
            level = "Level 3 - Nâng cao (Advanced)",
            evidenceUrl = (string?)null,
            evidenceFileUrls = Array.Empty<string>(),
            studentNote = "   ",
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, emptySubmit.StatusCode);

        // 3. Sinh viên nộp hồ sơ minh chứng hợp lệ -> 201 Created (PENDING)
        using var validSubmit = await student.PostAsJsonAsync("/api/v1/students/skills/requests", new
        {
            skillId,
            level = "Level 2 - Trung cấp (Intermediate)",
            evidenceUrl = "https://github.com/dntu-student/cloud-lab",
            evidenceFileUrls = new[] { $"/api/v1/files/view?key=uploads%2Fcert_{stamp}.png" },
            studentNote = "Hoàn thành lab thiết kế VPC và triển khai EKS.",
        }, Json);
        Assert.Equal(HttpStatusCode.Created, validSubmit.StatusCode);
        var submittedData = JsonDocument.Parse(await validSubmit.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        var requestId = submittedData.GetProperty("id").GetGuid();
        Assert.Equal("PENDING", submittedData.GetProperty("status").GetString());
        Assert.Equal(badgeUrl, submittedData.GetProperty("badgeImageUrl").GetString());

        // Trùng yêu cầu đang PENDING -> 409 Conflict
        using var duplicateSubmit = await student.PostAsJsonAsync("/api/v1/students/skills/requests", new
        {
            skillId,
            level = "Level 3 - Nâng cao (Advanced)",
            evidenceUrl = "https://github.com/dntu-student/duplicate",
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicateSubmit.StatusCode);

        // 4. Giảng viên thấy hồ sơ thật trong hàng chờ và yêu cầu bổ sung
        using var lecturerList = await lecturer.GetAsync("/api/v1/lecturer/skills/requests");
        Assert.Equal(HttpStatusCode.OK, lecturerList.StatusCode);
        var lecturerItems = JsonDocument.Parse(await lecturerList.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Contains(lecturerItems.EnumerateArray(), item => item.GetProperty("id").GetGuid() == requestId);

        using var requestChanges = await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer/skills/requests/{requestId}/request-changes",
            new { feedback = "Vui lòng bổ sung ảnh chụp cấu hình pipeline." }, Json);
        Assert.Equal(HttpStatusCode.OK, requestChanges.StatusCode);
        var changesData = JsonDocument.Parse(await requestChanges.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Equal("CHANGES_REQUESTED", changesData.GetProperty("status").GetString());

        // 5. Sinh viên bổ sung minh chứng -> quay lại PENDING
        using var resubmit = await student.PutAsJsonAsync($"/api/v1/students/skills/requests/{requestId}", new
        {
            level = "Level 3 - Nâng cao (Advanced)",
            evidenceUrl = "https://github.com/dntu-student/cloud-lab-v2",
            evidenceFileUrls = new[]
            {
                $"/api/v1/files/view?key=uploads%2Fcert_{stamp}.png",
                $"/api/v1/files/view?key=uploads%2Fpipeline_{stamp}.png",
            },
            studentNote = "Đã bổ sung ảnh pipeline CI/CD.",
        }, Json);
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        var resubmittedData = JsonDocument.Parse(await resubmit.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Equal("PENDING", resubmittedData.GetProperty("status").GetString());
        Assert.Equal(2, resubmittedData.GetProperty("evidenceFileUrls").GetArrayLength());

        // 6. Giảng viên phê duyệt & điều chỉnh cấp độ -> APPROVED + cấp VerifiedSkill
        using var approve = await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer/skills/requests/{requestId}/approve",
            new
            {
                level = "Level 4 - Chuyên gia (Expert)",
                feedback = "Minh chứng kiến trúc đầy đủ, đạt chuẩn ABET.",
            }, Json);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approvedData = JsonDocument.Parse(await approve.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Equal("APPROVED", approvedData.GetProperty("status").GetString());
        Assert.Equal("Level 4 - Chuyên gia (Expert)", approvedData.GetProperty("level").GetString());
        Assert.False(string.IsNullOrWhiteSpace(approvedData.GetProperty("lecturerName").GetString()));

        // Phê duyệt lại lần 2 -> 409 Conflict
        using var approveAgain = await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer/skills/requests/{requestId}/approve",
            new { feedback = "Thử duyệt lại" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, approveAgain.StatusCode);

        // VerifiedSkills & Skill Passport của sinh viên có huy hiệu mới + badgeImageUrl + verifiedByLecturerName
        using var verifiedSkills = await student.GetAsync("/api/v1/students/me/verified-skills");
        Assert.Equal(HttpStatusCode.OK, verifiedSkills.StatusCode);
        var verifiedArray = JsonDocument.Parse(await verifiedSkills.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        var verifiedEntry = verifiedArray.EnumerateArray().First(s => s.GetProperty("skillId").GetGuid() == skillId);
        Assert.Equal("EXPERT", verifiedEntry.GetProperty("level").GetString());
        Assert.Equal(badgeUrl, verifiedEntry.GetProperty("badgeImageUrl").GetString());
        Assert.Equal($"Giảng viên {stamp}", verifiedEntry.GetProperty("verifiedByLecturerName").GetString());

        using var passport = await student.GetAsync("/api/v1/students/me/skill-passport");
        Assert.Equal(HttpStatusCode.OK, passport.StatusCode);
        var passportSkills = JsonDocument.Parse(await passport.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("verifiedSkills");
        Assert.Contains(passportSkills.EnumerateArray(), s => s.GetProperty("skillId").GetGuid() == skillId);
    }

    [Fact]
    public async Task InactiveSkill_IsRejected_AndRejectedRequest_AllowsResubmitting()
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var admin = await RegisterAdminAsync($"rej-{stamp}");
        var student = await RegisterStudentAsync($"rej-{stamp}");
        var lecturer = await RegisterApprovedLecturerAsync(admin, $"rej-{stamp}");

        // Tạo 2 kỹ năng: 1 cái bị ngừng áp dụng, 1 cái bị lecturer từ chối rồi nộp lại
        using var inactiveCreate = await admin.PostAsJsonAsync("/api/v1/admin/catalog/skills", new
        {
            code = $"INACT_{stamp.ToUpperInvariant()}",
            name = $"Ngừng áp dụng {stamp}",
            category = "TECHNICAL",
        }, Json);
        Assert.Equal(HttpStatusCode.Created, inactiveCreate.StatusCode);
        var inactiveSkillId = JsonDocument.Parse(await inactiveCreate.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        using var deactivate = await admin.DeleteAsync($"/api/v1/admin/catalog/skills/{inactiveSkillId}");
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        using var submitInactive = await student.PostAsJsonAsync("/api/v1/students/skills/requests", new
        {
            skillId = inactiveSkillId,
            level = "Level 1 - Cơ bản (Beginner)",
            studentNote = "Thử nộp kỹ năng đã ngừng",
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, submitInactive.StatusCode);

        // Tạo kỹ năng active để test Reject -> Re-submit
        using var activeCreate = await admin.PostAsJsonAsync("/api/v1/admin/catalog/skills", new
        {
            code = $"REJ_{stamp.ToUpperInvariant()}",
            name = $"Chứng chỉ Test Reject {stamp}",
            category = "TECHNICAL",
        }, Json);
        Assert.Equal(HttpStatusCode.Created, activeCreate.StatusCode);
        var activeSkillId = JsonDocument.Parse(await activeCreate.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        using var firstSubmit = await student.PostAsJsonAsync("/api/v1/students/skills/requests", new
        {
            skillId = activeSkillId,
            level = "Level 2 - Trung cấp (Intermediate)",
            studentNote = "Minh chứng lần đầu",
        }, Json);
        Assert.Equal(HttpStatusCode.Created, firstSubmit.StatusCode);
        var firstRequestId = JsonDocument.Parse(await firstSubmit.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        using var reject = await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer/skills/requests/{firstRequestId}/reject",
            new { feedback = "Chứng chỉ không đúng định dạng." }, Json);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var rejectedData = JsonDocument.Parse(await reject.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Equal("REJECTED", rejectedData.GetProperty("status").GetString());

        // Sau khi bị REJECTED, sinh viên được nộp yêu cầu mới cho chính kỹ năng đó
        using var secondSubmit = await student.PostAsJsonAsync("/api/v1/students/skills/requests", new
        {
            skillId = activeSkillId,
            level = "Level 2 - Trung cấp (Intermediate)",
            evidenceUrl = "https://github.com/dntu-student/fixed-evidence",
            studentNote = "Nộp lại hồ sơ chuẩn chỉnh sau khi bị từ chối.",
        }, Json);
        Assert.Equal(HttpStatusCode.Created, secondSubmit.StatusCode);
    }

    private async Task<HttpClient> RegisterAdminAsync(string stamp)
    {
        var email = $"admin-sv-{stamp}@skillbridge.local";
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var role = await dbContext.Roles.SingleAsync(item => item.NormalizedName == RoleNames.Admin);
            var user = new User(email, $"Admin {stamp}", string.Empty);
            user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, Password));
            user.SetEmailVerified();
            user.AssignRole(role.Id);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        });

        var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> RegisterStudentAsync(string stamp)
    {
        var email = $"stu-sv-{stamp}@skillbridge.local";
        var client = factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = $"Sinh viên {stamp}",
            password = Password,
            accountType = "Student",
            studentCode = $"SV{Random.Shared.Next(100000, 999999)}",
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> RegisterApprovedLecturerAsync(HttpClient adminClient, string stamp)
    {
        var email = $"lec-sv-{stamp}@skillbridge.local";
        var client = factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = $"Giảng viên {stamp}",
            password = Password,
            accountType = "Lecturer",
            lecturerCode = $"GV{Random.Shared.Next(100000, 999999)}",
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        await using var db = factory.CreateDbContext();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        var lecturerId = await db.LecturerProfiles.Where(l => l.UserId == userId).Select(l => l.Id).SingleAsync();

        using var approve = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/lecturers/{lecturerId}/approve",
            new { note = "Approved for integration test" }, Json);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
