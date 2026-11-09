using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class Stage1And2EmpiricalChallengeTests(CatalogApiFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = DevelopmentTestAccountSeed.Password; // "SkillBridge.Test#2026"

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var lp = await dbContext.LecturerProfiles.FirstOrDefaultAsync(x => x.LecturerCode == "GV001");
            if (lp is not null)
            {
                lp.Update("GV-SEED-001", lp.Department, lp.AcademicTitle, lp.Bio, lp.WebsiteUrl, lp.OfficeLocation, lp.PhoneNumber, lp.IsProfilePublic, lp.ShowContactInfo);
                await dbContext.SaveChangesAsync();
            }
        });
    }

    private async Task EnsureSeedTestAccountsAsync()
    {
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var passwordHasher = new PasswordHasher<User>();
            await DevelopmentTestAccountSeed.SeedAsync(dbContext, passwordHasher, CancellationToken.None);
            var lp = await dbContext.LecturerProfiles.FirstOrDefaultAsync(x => x.LecturerCode == "GV001");
            if (lp is not null)
            {
                lp.Update("GV-SEED-001", lp.Department, lp.AcademicTitle, lp.Bio, lp.WebsiteUrl, lp.OfficeLocation, lp.PhoneNumber, lp.IsProfilePublic, lp.ShowContactInfo);
                await dbContext.SaveChangesAsync();
            }
        });
    }

    [Fact]
    public async Task TestAccounts_Student1_CanLogin_And_AccessProfile_And_Catalog_And_Skills()
    {
        await EnsureSeedTestAccountsAsync();

        using var client = factory.CreateClient();

        // 1. Authenticate as student1.test@skillbridge.local
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "student1.test@skillbridge.local",
            password = Password
        }, Json);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        using var loginDoc = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginDoc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // 2. GET /api/v1/auth/me
        var meResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // 3. GET /api/v1/students/me
        var profileResponse = await client.GetAsync("/api/v1/students/me");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        // 4. GET /api/v1/catalog/skills
        var skillsResponse = await client.GetAsync("/api/v1/catalog/skills");
        Assert.Equal(HttpStatusCode.OK, skillsResponse.StatusCode);
        using var skillsDoc = JsonDocument.Parse(await skillsResponse.Content.ReadAsStringAsync());
        var skills = skillsDoc.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.NotEmpty(skills);
        var firstSkillId = skills[0].GetProperty("id").GetString();

        // 5. GET /api/v1/catalog/faculties and majors
        var facultiesResponse = await client.GetAsync("/api/v1/catalog/faculties");
        Assert.Equal(HttpStatusCode.OK, facultiesResponse.StatusCode);

        var majorsResponse = await client.GetAsync("/api/v1/catalog/majors");
        Assert.Equal(HttpStatusCode.OK, majorsResponse.StatusCode);

        // 6. PUT /api/v1/students/me/skills (replace skills matching frontend payload)
        var replaceSkillsResponse = await client.PutAsJsonAsync("/api/v1/students/me/skills", new
        {
            skills = new[]
            {
                new { skillId = firstSkillId, level = "ADVANCED" }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, replaceSkillsResponse.StatusCode);

        // 7. GET /api/v1/students/me/skills
        var getSkillsResponse = await client.GetAsync("/api/v1/students/me/skills");
        Assert.Equal(HttpStatusCode.OK, getSkillsResponse.StatusCode);

        // 8. PUT /api/v1/students/me (Update student profile matching frontend UpdateStudentProfileInput)
        var updateProfileResponse = await client.PutAsJsonAsync("/api/v1/students/me", new
        {
            bio = "Sinh viên DNTU tài năng",
            academicYear = "2022-2026",
            phoneNumber = "0901234567",
            githubUrl = "https://github.com/student1"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updateProfileResponse.StatusCode);

        // 9. GET /api/v1/applications/me
        var myAppsResponse = await client.GetAsync("/api/v1/applications/me");
        Assert.Equal(HttpStatusCode.OK, myAppsResponse.StatusCode);
    }

    [Fact]
    public async Task TestAccounts_Company1_CanLogin_And_AccessApplications_And_CompanyProjects()
    {
        await EnsureSeedTestAccountsAsync();

        using var client = factory.CreateClient();

        // 1. Authenticate as company1.test@skillbridge.local
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "company1.test@skillbridge.local",
            password = Password
        }, Json);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        using var loginDoc = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginDoc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // 2. GET /api/v1/auth/me
        var meResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // 3. GET /api/v1/company/applications
        var companyAppsResponse = await client.GetAsync("/api/v1/company/applications");
        Assert.Equal(HttpStatusCode.OK, companyAppsResponse.StatusCode);

        // 4. GET /api/v1/company/projects
        var companyProjectsResponse = await client.GetAsync("/api/v1/company/projects");
        Assert.Equal(HttpStatusCode.OK, companyProjectsResponse.StatusCode);
    }

    [Fact]
    public async Task EndToEnd_Student1_And_Company1_Interaction_Without_400_Or_404()
    {
        await EnsureSeedTestAccountsAsync();

        // Step A: Company1 publishes a project
        using var companyClient = factory.CreateClient();
        var companyLogin = await companyClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "company1.test@skillbridge.local",
            password = Password
        }, Json);
        Assert.Equal(HttpStatusCode.OK, companyLogin.StatusCode);
        var companyToken = (await companyLogin.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("accessToken").GetString();
        companyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyToken);

        // Get an active skill
        var skillResponse = await companyClient.GetAsync("/api/v1/catalog/skills");
        var skillDoc = JsonDocument.Parse(await skillResponse.Content.ReadAsStringAsync());
        var skillId = skillDoc.RootElement.GetProperty("data")[0].GetProperty("id").GetString();

        // Create project as Company1
        var projectTitle = $"Interaction Project {Guid.NewGuid():N}"[..25];
        var createProjectResp = await companyClient.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title = projectTitle,
            summary = "Dự án kiểm thử tương tác sinh viên và doanh nghiệp",
            problemStatement = "Vấn đề thực tế cần giải quyết.",
            businessRequirements = "Yêu cầu nghiệp vụ rõ ràng.",
            technicalConstraints = "Ràng buộc kỹ thuật .NET.",
            difficulty = "INTERMEDIATE",
            workType = "HYBRID",
            durationWeeks = 12,
            expectedStudentCount = 2,
            minTeamSize = 1,
            maxTeamSize = 4,
            applicationDeadline = DateTimeOffset.UtcNow.AddDays(30),
            skills = new[]
            {
                new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }
            },
            deliverables = new[]
            {
                new { name = "Báo cáo giai đoạn 1", description = "Đặc tả yêu cầu" }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createProjectResp.StatusCode);
        var projectData = (await createProjectResp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("data");
        var projectId = projectData.GetProperty("id").GetString();
        var projectSlug = projectData.GetProperty("slug").GetString();

        // Submit project for approval
        var submitResp = await companyClient.PostAsync($"/api/v1/company/projects/{projectId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // Admin approves and publishes the project
        using var adminClient = factory.CreateClient();
        var adminLogin = await adminClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin.test@skillbridge.local",
            password = Password
        }, Json);
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        var adminToken = (await adminLogin.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("accessToken").GetString();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var approveResp = await adminClient.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        // Step B: Student1 searches and details the project
        using var studentClient = factory.CreateClient();
        var studentLogin = await studentClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "student1.test@skillbridge.local",
            password = Password
        }, Json);
        Assert.Equal(HttpStatusCode.OK, studentLogin.StatusCode);
        var studentToken = (await studentLogin.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("accessToken").GetString();
        studentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        // Get by slug
        var getBySlugResp = await studentClient.GetAsync($"/api/v1/projects/{projectSlug}");
        Assert.Equal(HttpStatusCode.OK, getBySlugResp.StatusCode);

        // Get by GUID (testing the dual slug/GUID capability added in M2)
        var getByGuidResp = await studentClient.GetAsync($"/api/v1/projects/{projectId}");
        Assert.Equal(HttpStatusCode.OK, getByGuidResp.StatusCode);

        // Student applies to the project
        var applyResp = await studentClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new
        {
            coverLetter = "Em xin ứng tuyển vào dự án của công ty."
        }, Json);
        Assert.Equal(HttpStatusCode.Created, applyResp.StatusCode);
        var appData = (await applyResp.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("data");
        var applicationId = appData.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(applicationId));

        // Step C: Company views applications and shortlists the student
        var compListAppsResp = await companyClient.GetAsync($"/api/v1/company/applications?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, compListAppsResp.StatusCode);
        var appsListDoc = JsonDocument.Parse(await compListAppsResp.Content.ReadAsStringAsync());
        var apps = appsListDoc.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.Contains(apps, a => a.GetProperty("id").GetString() == applicationId);

        // Company shortlists
        var shortlistResp = await companyClient.PostAsJsonAsync(
            $"/api/v1/company/applications/{applicationId}/shortlist",
            new { reason = "Hồ sơ ấn tượng, đưa vào shortlist" },
            Json);
        Assert.Equal(HttpStatusCode.OK, shortlistResp.StatusCode);

        // Company accepts application
        var acceptResp = await companyClient.PostAsJsonAsync(
            $"/api/v1/company/applications/{applicationId}/accept",
            new { reason = "Chính thức đồng ý tiếp nhận sinh viên" },
            Json);
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // Step D: Student verifies application status is ACCEPTED
        var myAppDetailResp = await studentClient.GetAsync($"/api/v1/applications/{applicationId}");
        Assert.Equal(HttpStatusCode.OK, myAppDetailResp.StatusCode);
        var appDetailDoc = JsonDocument.Parse(await myAppDetailResp.Content.ReadAsStringAsync());
        var status = appDetailDoc.RootElement.GetProperty("data").GetProperty("status").GetString();
        Assert.Equal("ACCEPTED", status);
    }
}
