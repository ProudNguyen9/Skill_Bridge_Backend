using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// End-to-end integration tests verifying real workflows defined in Cac_luong_nghiep_vu_DNTU_SkillBridge_chuan:
/// Steps 13-35 and Step 64 notifications (student search, team invite by MSSV/email, group consent &amp; submit,
/// duplicate conflict resolution, adjustment request/confirm, merge individuals into team, lecturer directory/invite/decline/re-invite/accept,
/// lecturer team recommendations, and commitment confirm/decline opening workspace).
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class RealWorkflowEndToEndTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";
    private readonly string prefix = $"rw{Guid.NewGuid():N}"[..10];

    [Fact]
    public async Task TeamCreation_SearchByMssvAndInvite_MemberConsent_Submit_AndNotificationFlow()
    {
        var (company, admin) = await CreateCompanyAndAdminAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Team Consent Project");

        var (leader, leaderStudentId, _) = await RegisterStudentWithProfileAsync("leader");
        var (member, memberStudentId, memberCode) = await RegisterStudentWithProfileAsync("member");

        // Search member by MSSV
        var searchResults = (await leader.GetFromJsonAsync<ListEnvelope<StudentSearchDto>>(
            $"/api/v1/students/search?q={Uri.EscapeDataString(memberCode)}&limit=10", Json))!.Data;
        Assert.Contains(searchResults, item => item.StudentId == memberStudentId && item.StudentCode == memberCode);

        // Create team (Bước 13)
        var createTeam = await leader.PostAsJsonAsync("/api/v1/teams", new
        {
            name = $"{prefix} Alpha Team",
            description = "Nhóm chuyên Backend & AI",
            teamType = "PROJECT",
            targetProjectId = projectId,
            leaderFunctionalRole = "Team Lead / Backend"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createTeam.StatusCode);
        var team = (await createTeam.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;

        // Invite member by MSSV identifier (Bước 14)
        var inviteResp = await leader.PostAsJsonAsync($"/api/v1/teams/{team.Id}/invitations", new
        {
            identifier = memberCode,
            role = "MEMBER",
            functionalRole = "Frontend React",
            message = "Tham gia nhóm cùng làm bài toán doanh nghiệp",
            expiresInDays = 7
        }, Json);
        Assert.Equal(HttpStatusCode.OK, inviteResp.StatusCode);
        var invitation = (await inviteResp.Content.ReadFromJsonAsync<Envelope<InvitationDto>>(Json))!.Data;

        // Member receives notification & accepts invitation
        var memberNotifications = (await member.GetFromJsonAsync<ListEnvelope<NotificationDto>>("/api/v1/notifications", Json))!.Data;
        Assert.NotEmpty(memberNotifications);

        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/team-invitations/{invitation.Id}/accept", null)).StatusCode);

        // Leader prepares group application requiring member consent (Bước 15)
        var applyResp = await leader.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new
        {
            teamId = team.Id,
            coverLetter = "Nhóm chúng em sẵn sàng triển khai bài toán.",
            githubUrl = "https://github.com/dntu/alpha-team",
            cvUrl = "https://files.example.com/team-cv.pdf",
            cvFileName = "team-cv.pdf",
            availability = "20 giờ/tuần",
            requireMemberConsent = true,
            members = new[]
            {
                new { studentId = leaderStudentId, proposedRole = "Team Lead" },
                new { studentId = memberStudentId, proposedRole = "Frontend" }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, applyResp.StatusCode);
        var application = (await applyResp.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal("WAITING_MEMBER_CONSENT", application.Status);

        // Not visible in company pending queue before leader submits; member sees it in group-consents
        var consents = (await member.GetFromJsonAsync<ListEnvelope<ApplicationDto>>("/api/v1/applications/me/group-consents", Json))!.Data;
        Assert.Contains(consents, a => a.Id == application.Id && a.Status == "WAITING_MEMBER_CONSENT");

        // Leader cannot submit while member hasn't consented
        Assert.Equal(HttpStatusCode.Conflict, (await leader.PostAsync($"/api/v1/applications/{application.Id}/submit", null)).StatusCode);

        // Member confirms consent
        var consentResp = await member.PostAsJsonAsync($"/api/v1/applications/{application.Id}/member-consent", new
        {
            approve = true,
            note = "Đồng ý chia sẻ hồ sơ"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, consentResp.StatusCode);

        // Leader submits group application (Bước 16)
        var submitResp = await leader.PostAsync($"/api/v1/applications/{application.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitted = (await submitResp.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal("PENDING", submitted.Status);

        // Company receives application
        var companyApps = (await company.GetFromJsonAsync<CompanyPagedDto>(
            $"/api/v1/company/applications?projectId={projectId}&applicationType=TEAM", Json))!;
        Assert.Contains(companyApps.Data, item => item.Id == application.Id && item.ApplicationType == "TEAM");
    }

    [Fact]
    public async Task ConflictCheck_Resolve_AdjustmentFlow_And_CommitmentConfirmToWorkspace()
    {
        var (company, admin) = await CreateCompanyAndAdminAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Adjustment Conflict");

        var (leader, leaderStudentId, _) = await RegisterStudentWithProfileAsync("adjlead");
        var (member, memberStudentId, _) = await RegisterStudentWithProfileAsync("adjmem");

        // Member first applies individually (Bước 17)
        var indivResp = await member.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new
        {
            coverLetter = "Ứng tuyển cá nhân trước"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, indivResp.StatusCode);
        var indivApp = (await indivResp.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        // Leader forms team with member and submits team application without blocking
        var team = (await (await leader.PostAsJsonAsync("/api/v1/teams", new { name = $"{prefix} Adj Team" }, Json))
            .Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;
        var inv = (await (await leader.PostAsJsonAsync($"/api/v1/teams/{team.Id}/invitations", new { studentId = memberStudentId }, Json))
            .Content.ReadFromJsonAsync<Envelope<InvitationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/team-invitations/{inv.Id}/accept", null)).StatusCode);

        var teamApplyResp = await leader.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new
        {
            teamId = team.Id,
            coverLetter = "Ứng tuyển theo nhóm",
            requireMemberConsent = false,
            members = new[]
            {
                new { studentId = leaderStudentId, proposedRole = "Backend" },
                new { studentId = memberStudentId, proposedRole = "UI/UX" }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, teamApplyResp.StatusCode);
        var teamApp = (await teamApplyResp.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        // Check conflict for member (Bước 18)
        var conflicts = (await member.GetFromJsonAsync<ListEnvelope<ConflictDto>>(
            $"/api/v1/projects/{projectId}/application-conflicts", Json))!.Data;
        Assert.True(conflicts.Count >= 2);

        // Member resolves conflict preferring team and withdrawing individual app
        var resolveResp = await member.PostAsJsonAsync($"/api/v1/applications/{teamApp.Id}/resolve-conflict", new
        {
            preferred = true,
            priorityOrder = 1,
            note = "Ưu tiên đi theo nhóm",
            withdrawOtherApplicationId = indivApp.Id
        }, Json);
        Assert.Equal(HttpStatusCode.OK, resolveResp.StatusCode);

        // Company requests adjustment on team roles (Bước 21)
        var adjResp = await company.PostAsJsonAsync($"/api/v1/company/applications/{teamApp.Id}/request-adjustment", new
        {
            reason = "Đề nghị bổ sung rõ vai trò QA",
            selectedStudentIds = new[] { leaderStudentId, memberStudentId },
            memberRoles = new Dictionary<Guid, string>
            {
                [leaderStudentId] = "Tech Lead",
                [memberStudentId] = "QA & Frontend"
            }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, adjResp.StatusCode);
        var adjApp = (await adjResp.Content.ReadFromJsonAsync<Envelope<CompanyApplicationDto>>(Json))!.Data;
        Assert.Equal("ADJUSTMENT_REQUESTED", adjApp.Status);

        // Leader confirms adjustment
        var confirmAdj = await leader.PostAsync($"/api/v1/applications/{teamApp.Id}/confirm-adjustment", null);
        Assert.Equal(HttpStatusCode.OK, confirmAdj.StatusCode);

        // Company accepts team application -> creates commitments (Bước 30)
        var acceptResp = await company.PostAsJsonAsync($"/api/v1/company/applications/{teamApp.Id}/accept", new
        {
            reason = "Phù hợp yêu cầu",
            conditions = "Hoàn thành sprint mỗi 2 tuần",
            confirmationDeadlineDays = 5
        }, Json);
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // Leader and member check commitments overview & confirm (Bước 31-35)
        var overview = (await leader.GetFromJsonAsync<Envelope<CommitmentOverviewDto>>(
            $"/api/v1/projects/{projectId}/commitments/overview", Json))!.Data;
        Assert.Equal(2, overview.Members.Count);

        Assert.Equal(HttpStatusCode.OK, (await leader.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);

        // Project moves to IN_PROGRESS and workspace is open for both
        var wsLeader = await leader.GetAsync($"/api/v1/workspaces/{projectId}/overview");
        Assert.Equal(HttpStatusCode.OK, wsLeader.StatusCode);
        var wsMember = await member.GetAsync($"/api/v1/workspaces/{projectId}/overview");
        Assert.Equal(HttpStatusCode.OK, wsMember.StatusCode);
    }

    [Fact]
    public async Task Company_CanMergeIndividualApplications_And_LecturerCanRecommendAndBeInvited()
    {
        var (company, admin) = await CreateCompanyAndAdminAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Merge & Lecturer");

        var (s1, s1Id, _) = await RegisterStudentWithProfileAsync("ind1");
        var (s2, s2Id, _) = await RegisterStudentWithProfileAsync("ind2");
        var (s3, s3Id, _) = await RegisterStudentWithProfileAsync("decline");

        // Lecturer directory & recommendations (Bước 23-29)
        var (lecturerClient, lecturerId) = await CreateApprovedLecturerAsync();

        var directory = (await s1.GetFromJsonAsync<ListEnvelope<LecturerDirectoryDto>>(
            "/api/v1/lecturers/directory?limit=20", Json))!.Data;
        Assert.Contains(directory, item => item.LecturerId == lecturerId);

        // Create a team & lecturer recommends it
        var teamResp = await s1.PostAsJsonAsync("/api/v1/teams", new { name = $"{prefix} Rec Team" }, Json);
        var recTeam = (await teamResp.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;

        var recCreate = await lecturerClient.PostAsJsonAsync("/api/v1/lecturers/recommendations", new
        {
            teamId = recTeam.Id,
            projectId,
            reason = "Nhóm có năng lực chuyên môn phù hợp bài toán"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, recCreate.StatusCode);
        var rec = (await recCreate.Content.ReadFromJsonAsync<Envelope<RecommendationDto>>(Json))!.Data;

        // Leader sees recommendation and accepts it
        var myRecs = (await s1.GetFromJsonAsync<ListEnvelope<RecommendationDto>>("/api/v1/students/me/team-recommendations", Json))!.Data;
        Assert.Contains(myRecs, r => r.Id == rec.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await s1.PostAsJsonAsync($"/api/v1/team-recommendations/{rec.Id}/respond", new
        {
            accept = true,
            note = "Nhóm sẽ ứng tuyển ngay"
        }, Json)).StatusCode);

        // 3 students apply individually
        var a1 = (await (await s1.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { coverLetter = "Backend dev", recommendedByLecturerId = lecturerId }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        var a2 = (await (await s2.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { coverLetter = "Frontend dev" }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        var a3 = (await (await s3.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { coverLetter = "Also applying" }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        // Company merges a1 + a2 into a new project team (Bước 22)
        var mergeResp = await company.PostAsJsonAsync($"/api/v1/company/projects/{projectId}/merge-applications", new
        {
            applicationIds = new[] { a1.Id, a2.Id },
            teamName = $"{prefix} Merged Dream Team",
            leaderStudentId = s1Id,
            memberRoles = new Dictionary<Guid, string>
            {
                [s1Id] = "Leader / API",
                [s2Id] = "Frontend Lead"
            },
            conditions = "Cam kết tham gia đủ 4 tuần",
            confirmationDeadlineDays = 5
        }, Json);
        Assert.Equal(HttpStatusCode.OK, mergeResp.StatusCode);

        // Company proposes adding s3 into the existing merged team and team leader s1 approves before s3 declines commitment (Bước 34)
        var requestJoinS3 = await company.PostAsJsonAsync(
            $"/api/v1/company/applications/{a3.Id}/request-join-team",
            new { proposedRole = "QA Engineer" },
            Json);
        Assert.Equal(HttpStatusCode.OK, requestJoinS3.StatusCode);
        var leaderApproveS3 = await s1.PostAsJsonAsync(
            $"/api/v1/applications/{a3.Id}/leader-join-approval",
            new { approve = true, note = "Đồng ý nhận thêm thành viên" },
            Json);
        Assert.Equal(HttpStatusCode.OK, leaderApproveS3.StatusCode);
        var declineCommitment = await s3.PostAsJsonAsync($"/api/v1/projects/{projectId}/commitment/decline", new
        {
            reason = "Trùng lịch thực tập khác"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, declineCommitment.StatusCode);

        // s1 & s2 confirm commitments -> s1 invites lecturer (Bước 27)
        Assert.Equal(HttpStatusCode.OK, (await s1.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s2.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);

        var inviteLect = await s1.PostAsJsonAsync($"/api/v1/projects/{projectId}/lecturer-invitations", new
        {
            lecturerId,
            role = "PRIMARY",
            expectedWork = "Review kiến trúc và hướng dẫn kỹ thuật hàng tuần",
            expectedDuration = "4 tuần",
            allowanceShareAmount = 300000,
            isCourseLinked = true,
            courseName = "Đồ án chuyên ngành CNTT"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, inviteLect.StatusCode);
        var assign1 = (await inviteLect.Content.ReadFromJsonAsync<Envelope<LecturerAssignmentDto>>(Json))!.Data;
        Assert.Equal("INVITED", assign1.Status);

        // Duplicate invite while INVITED returns descriptive 409 Conflict ProblemDetails,
        // and /api/v1/lecturers/invitations/sent lists the invitation for both student & company
        var duplicateInvite = await s1.PostAsJsonAsync($"/api/v1/projects/{projectId}/lecturer-invitations", new
        {
            lecturerId,
            role = "PRIMARY",
            expectedWork = "Mời trùng lần 2 khi chưa phản hồi"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicateInvite.StatusCode);
        var conflictBody = await duplicateInvite.Content.ReadAsStringAsync();
        Assert.Contains("lời mời", conflictBody, StringComparison.OrdinalIgnoreCase);

        var studentSent = (await (await s1.GetAsync("/api/v1/lecturers/invitations/sent"))
            .Content.ReadFromJsonAsync<Envelope<List<LecturerAssignmentDto>>>(Json))!.Data;
        Assert.Contains(studentSent, x => x.Id == assign1.Id && x.Status == "INVITED");

        var companySent = (await (await company.GetAsync("/api/v1/lecturers/invitations/sent"))
            .Content.ReadFromJsonAsync<Envelope<List<LecturerAssignmentDto>>>(Json))!.Data;
        Assert.Contains(companySent, x => x.Id == assign1.Id && x.Status == "INVITED");

        // Lecturer declines first invite with reason (Bước 28)
        var declLect = await lecturerClient.PutAsJsonAsync($"/api/v1/lecturers/me/assignments/{assign1.Id}/decline", new
        {
            reason = "Đề nghị điều chỉnh lịch họp sang thứ 6"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, declLect.StatusCode);
        Assert.Equal("DECLINED", (await declLect.Content.ReadFromJsonAsync<Envelope<LecturerAssignmentDto>>(Json))!.Data.Status);

        // Re-invite same lecturer after decline (filtered unique index allows it!) and lecturer accepts (Bước 28-29)
        var reInvite = await s1.PostAsJsonAsync($"/api/v1/projects/{projectId}/lecturer-invitations", new
        {
            lecturerId,
            role = "PRIMARY",
            expectedWork = "Hướng dẫn kỹ thuật vào thứ 6 hàng tuần",
            expectedDuration = "4 tuần",
            allowanceShareAmount = 350000
        }, Json);
        Assert.Equal(HttpStatusCode.Created, reInvite.StatusCode);
        var assign2 = (await reInvite.Content.ReadFromJsonAsync<Envelope<LecturerAssignmentDto>>(Json))!.Data;

        var acceptLect = await lecturerClient.PutAsync($"/api/v1/lecturers/me/assignments/{assign2.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptLect.StatusCode);
        Assert.Equal("ACTIVE", (await acceptLect.Content.ReadFromJsonAsync<Envelope<LecturerAssignmentDto>>(Json))!.Data.Status);
    }

    private async Task<(HttpClient Company, HttpClient Admin)> CreateCompanyAndAdminAsync()
    {
        var email = $"rw-co-{Guid.NewGuid():N}@skillbridge.local";
        var company = factory.CreateClient();
        await company.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = "DN Kiểm thử", password = Password, accountType = "Company" }, Json);
        var auth = (await (await company.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json))
            .Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        company.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var companyName = $"DN {prefix} {Guid.NewGuid():N}"[..30];
        await company.PutAsJsonAsync("/api/v1/companies/me", new { name = companyName }, Json);
        await factory.RunWithDbContextAsync(async db =>
        {
            var c = await db.Companies.SingleAsync(x => x.Name == companyName);
            c.MarkVerified(DateTimeOffset.UtcNow, null);
            await db.SaveChangesAsync();
        });

        Guid adminUserId = default;
        await factory.RunWithDbContextAsync(async db =>
        {
            var user = new User($"rw-admin-{Guid.NewGuid():N}@skillbridge.local", "Admin RW", "seed-hash");
            db.Users.Add(user);
            await db.SaveChangesAsync();
            adminUserId = user.Id;
        });
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", CreateRoleToken(RoleNames.Admin, adminUserId));
        return (company, admin);
    }

    private async Task<Guid> PublishProjectAsync(HttpClient company, HttpClient admin, string title)
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<ListEnvelope<CatalogSkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        var skillId = skills.First().Id;

        var created = await company.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title,
            difficulty = "BEGINNER",
            workType = "HYBRID",
            durationWeeks = 4,
            expectedStudentCount = 4,
            minTeamSize = 2,
            maxTeamSize = 4,
            skills = new[] { new { skillId, requirementLevel = "MUST_HAVE", isRequired = true } },
            deliverables = new[] { new { name = "Sản phẩm MVP", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var projectId = (await created.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data.Id;

        await company.PutAsJsonAsync($"/api/v1/company/projects/{projectId}", new
        {
            title,
            summary = $"Tóm tắt {title}",
            problemStatement = "Bài toán thực tế",
            businessRequirements = "Yêu cầu rõ ràng",
            technicalConstraints = ".NET + Next.js",
            difficulty = "BEGINNER",
            workType = "HYBRID",
            durationWeeks = 4,
            applicationDeadline = DateTimeOffset.UtcNow.AddDays(14),
            expectedStudentCount = 4,
            minTeamSize = 2,
            maxTeamSize = 4,
            allowanceAmount = 4_000_000,
            allowanceCurrency = "VND",
            skills = new[] { new { skillId, requirementLevel = "MUST_HAVE", isRequired = true } },
            deliverables = new[] { new { name = "Sản phẩm MVP", description = (string?)null } }
        }, Json);

        Assert.Equal(HttpStatusCode.OK, (await company.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);
        return projectId;
    }

    private async Task<(HttpClient Client, Guid StudentProfileId, string StudentCode)> RegisterStudentWithProfileAsync(string label)
    {
        var email = $"rw-{label}-{Guid.NewGuid():N}@skillbridge.local";
        var code = "22" + Random.Shared.Next(100000, 999999);
        var client = factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = $"SV {label} {code}",
            studentCode = code,
            password = Password,
            accountType = "Student"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var auth = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json))
            .Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var profile = (await client.GetFromJsonAsync<Envelope<StudentProfileDto>>("/api/v1/students/me", Json))!.Data;
        return (client, profile.Id, profile.StudentCode ?? code);
    }

    private async Task<(HttpClient Client, Guid LecturerProfileId)> CreateApprovedLecturerAsync()
    {
        Guid lecturerUserId = default;
        await factory.RunWithDbContextAsync(async db =>
        {
            var user = new User($"rw-lect-{Guid.NewGuid():N}@skillbridge.local", $"TS. GV {prefix}", "seed-hash");
            var role = await db.Roles.SingleAsync(x => x.NormalizedName == RoleNames.Lecturer);
            user.AssignRole(role.Id);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            lecturerUserId = user.Id;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateRoleToken(RoleNames.Lecturer, lecturerUserId));
        var profile = (await client.GetFromJsonAsync<Envelope<LecturerProfileDto>>("/api/v1/lecturers/me", Json))!.Data;
        await client.PutAsJsonAsync("/api/v1/lecturers/me", new
        {
            lecturerCode = $"GV{Random.Shared.Next(100, 999)}",
            department = "Khoa CNTT",
            academicTitle = "Tiến sĩ",
            isProfilePublic = true,
            showContactInfo = true
        }, Json);
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
        foreach (var permission in DNTU.SkillBridge.Infrastructure.Persistence.Seed.IdentitySeed.GetPermissionsForRole(role))
        {
            claims.Add(new Claim("permission", permission));
        }
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record Envelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);
    private sealed record AuthDto(string AccessToken);
    private sealed record CatalogSkillDto(Guid Id);
    private sealed record ProjectDetailDto(Guid Id);
    private sealed record StudentProfileDto(Guid Id, string? StudentCode);
    private sealed record StudentSearchDto(Guid StudentId, string DisplayName, string? StudentCode);
    private sealed record TeamDto(Guid Id, string Name);
    private sealed record InvitationDto(Guid Id, string Status);
    private sealed record NotificationDto(Guid Id, string Type, string Title);
    private sealed record ApplicationDto(Guid Id, Guid ProjectId, string Status, string ApplicationType);
    private sealed record ConflictDto(Guid ApplicationId, Guid ProjectId);
    private sealed record CompanyApplicationDto(Guid Id, string Status, string ApplicationType);
    private sealed record MetaDto(int TotalItems);
    private sealed record CompanyPagedDto(IReadOnlyCollection<CompanyApplicationDto> Data, MetaDto Meta);
    private sealed record CommitmentOverviewDto(Guid ProjectId, IReadOnlyCollection<CommitmentMemberDto> Members);
    private sealed record CommitmentMemberDto(Guid StudentId, string Status);
    private sealed record LecturerProfileDto(Guid Id);
    private sealed record LecturerDirectoryDto(Guid LecturerId, string DisplayName);
    private sealed record LecturerAssignmentDto(Guid Id, string Status);
    private sealed record RecommendationDto(Guid Id, Guid TeamId, Guid ProjectId);
}
