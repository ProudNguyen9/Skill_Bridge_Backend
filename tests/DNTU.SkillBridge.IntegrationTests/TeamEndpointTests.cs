using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

[Collection(CatalogApiCollection.Name)]
public sealed class TeamEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";

    [Fact]
    public async Task Leader_CanInvite_AndStudentCanAcceptInvitation()
    {
        var leader = await RegisterStudentAsync("leader");
        var member = await RegisterStudentAsync("member");

        var memberTeam = await member.PostAsJsonAsync("/api/v1/teams", new { name = "Nhóm thành viên" }, Json);
        Assert.Equal(HttpStatusCode.Created, memberTeam.StatusCode);

        var leaderTeamResponse = await leader.PostAsJsonAsync("/api/v1/teams", new { name = "Nhóm dự án" }, Json);
        Assert.Equal(HttpStatusCode.Created, leaderTeamResponse.StatusCode);
        var leaderTeam = (await leaderTeamResponse.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;

        var memberUserId = await GetUserIdAsync(member);
        Guid memberProfileId = default;
        await factory.RunWithDbContextAsync(async db =>
        {
            memberProfileId = await db.StudentProfiles.AsNoTracking()
                .Where(profile => profile.UserId == memberUserId)
                .Select(profile => profile.Id)
                .SingleAsync();
        });

        var invite = await leader.PostAsJsonAsync($"/api/v1/teams/{leaderTeam.Id}/invitations", new
        {
            studentId = memberProfileId,
            role = "MEMBER",
            expiresInDays = 7
        }, Json);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var invitation = (await invite.Content.ReadFromJsonAsync<Envelope<InvitationDto>>(Json))!.Data;

        var invitations = (await (await member.GetAsync("/api/v1/students/me/team-invitations"))
            .Content.ReadFromJsonAsync<Envelope<IReadOnlyCollection<InvitationDto>>>(Json))!.Data;
        Assert.Contains(invitations, item => item.Id == invitation.Id && item.Status == "PENDING");

        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/team-invitations/{invitation.Id}/accept", null)).StatusCode);
        var members = (await (await leader.GetAsync($"/api/v1/teams/{leaderTeam.Id}/members"))
            .Content.ReadFromJsonAsync<Envelope<IReadOnlyCollection<MemberDto>>>(Json))!.Data;
        Assert.Contains(members, item => item.StudentId == memberProfileId && item.Role == "MEMBER");
    }

    [Fact]
    public async Task NonLeader_CannotManage_Team()
    {
        var leader = await RegisterStudentAsync("owner");
        var other = await RegisterStudentAsync("other");
        var created = await leader.PostAsJsonAsync("/api/v1/teams", new { name = "Nhóm riêng" }, Json);
        var team = (await created.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/teams/{team.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/v1/teams/{team.Id}", new { name = "Chiếm quyền" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Leader_CanSetAndUpdateTeamAvatar_EvenWhenLocked()
    {
        var leader = await RegisterStudentAsync("avatar-leader");
        var outsider = await RegisterStudentAsync("avatar-outsider");

        var created = await leader.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Nhóm có ảnh đại diện",
            avatarUrl = "/assets/team-1.png"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var team = (await created.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;
        Assert.Equal("/assets/team-1.png", team.AvatarUrl);

        await factory.RunWithDbContextAsync(async db =>
        {
            var entity = await db.Teams.SingleAsync(item => item.Id == team.Id);
            entity.Lock();
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.PutAsJsonAsync($"/api/v1/teams/{team.Id}/avatar", new { avatarUrl = "/assets/hack.png" }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict,
            (await leader.PutAsJsonAsync($"/api/v1/teams/{team.Id}", new { name = "Đổi tên khi khóa" }, Json)).StatusCode);

        var updated = await leader.PutAsJsonAsync($"/api/v1/teams/{team.Id}/avatar", new
        {
            avatarUrl = "/api/v1/files/view?key=teams/avatar.png"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedTeam = (await updated.Content.ReadFromJsonAsync<Envelope<TeamDto>>(Json))!.Data;
        Assert.Equal("/api/v1/files/view?key=teams/avatar.png", updatedTeam.AvatarUrl);

        await factory.RunWithDbContextAsync(async db =>
        {
            var entity = await db.Teams.SingleAsync(item => item.Id == team.Id);
            Assert.True(entity.IsLocked);
        });

        var fetched = (await leader.GetFromJsonAsync<Envelope<TeamDto>>($"/api/v1/teams/{team.Id}", Json))!.Data;
        Assert.Equal("/api/v1/files/view?key=teams/avatar.png", fetched.AvatarUrl);
    }

    [Fact]
    public async Task Team_CanOnlyHaveOneActiveProject_UntilCompleted()
    {
        var company = await CreateVerifiedCompanyAsync();
        var admin = await CreateAdminClientAsync();
        var firstProjectId = await PublishProjectAsync(company, admin, $"Dự án chính 1 {Guid.NewGuid():N}"[..24]);
        var secondProjectId = await PublishProjectAsync(company, admin, $"Dự án chính 2 {Guid.NewGuid():N}"[..24]);

        var leader = await RegisterStudentAsync("single-leader");
        var member = await RegisterStudentAsync("single-member");
        var leaderProfileId = await EnsureStudentProfileAsync(leader);
        var memberProfileId = await EnsureStudentProfileAsync(member);

        Guid teamId = default;
        await factory.RunWithDbContextAsync(async db =>
        {
            var team = new DNTU.SkillBridge.Domain.Teams.Team("Nhóm Một Dự Án", leaderProfileId, avatarUrl: "/assets/team-main.png");
            db.Teams.Add(team);
            db.TeamMembers.Add(new DNTU.SkillBridge.Domain.Teams.TeamMember(team.Id, memberProfileId, DNTU.SkillBridge.Domain.Teams.TeamMemberRole.MEMBER));
            await db.SaveChangesAsync();
            teamId = team.Id;
        });

        var firstApply = await leader.PostAsJsonAsync($"/api/v1/projects/{firstProjectId}/applications", new { teamId }, Json);
        Assert.Equal(HttpStatusCode.Created, firstApply.StatusCode);
        var firstApp = (await firstApply.Content.ReadFromJsonAsync<Envelope<ApplicationIdDto>>(Json))!.Data;

        var busyDuringApply = (await leader.GetFromJsonAsync<Envelope<TeamDto>>($"/api/v1/teams/{teamId}", Json))!.Data;
        Assert.True(busyDuringApply.HasActiveProject);
        Assert.Equal(firstProjectId, busyDuringApply.ActiveProjectId);
        Assert.Equal("APPLYING", busyDuringApply.ActiveProjectPhase);

        // Cannot apply to another project while first application is active
        Assert.Equal(HttpStatusCode.Conflict,
            (await leader.PostAsJsonAsync($"/api/v1/projects/{secondProjectId}/applications", new { teamId }, Json)).StatusCode);

        // Accept first project -> team becomes locked on active IN_PROGRESS project
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{firstApp.Id}/accept", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await leader.PostAsync($"/api/v1/projects/{firstProjectId}/commitment/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await member.PostAsync($"/api/v1/projects/{firstProjectId}/commitment/confirm", null)).StatusCode);

        var activeDetail = (await leader.GetFromJsonAsync<Envelope<TeamDto>>($"/api/v1/teams/{teamId}", Json))!.Data;
        Assert.True(activeDetail.IsLocked);
        Assert.True(activeDetail.HasActiveProject);
        Assert.Equal(firstProjectId, activeDetail.ActiveProjectId);
        Assert.Equal("IN_PROGRESS", activeDetail.ActiveProjectPhase);

        Assert.Equal(HttpStatusCode.Conflict,
            (await leader.PostAsJsonAsync($"/api/v1/projects/{secondProjectId}/applications", new { teamId }, Json)).StatusCode);

        // Seed completion prerequisites and complete first project via admin API -> team is released for a new project
        await factory.RunWithDbContextAsync(async db =>
        {
            var submission = new ProjectSubmission(
                firstProjectId,
                null,
                leaderProfileId,
                new SubmissionVersion("Hoàn thành dự án 1", "https://github.com/dntu/skillbridge-team-test", null, null, null));
            submission.ApproveTechnical(submission.Version);
            submission.AcceptBusiness(submission.Version);
            db.ProjectSubmissions.Add(submission);

            var rubric = await db.Rubrics.FirstOrDefaultAsync();
            if (rubric is null)
            {
                var lp = new LecturerProfile(await GetUserIdAsync(leader));
                db.LecturerProfiles.Add(lp);
                rubric = new Rubric(lp.Id, "Rubric Test Team");
                rubric.AddCriterion("Hoàn thành", 100.00m, 1);
                rubric.Lock();
                db.Rubrics.Add(rubric);
                await db.SaveChangesAsync();
            }

            var evaluatorUserId = await GetUserIdAsync(leader);
            foreach (var sid in new[] { leaderProfileId, memberProfileId })
            {
                var eval = new AcademicEvaluation(firstProjectId, sid, rubric.Id, evaluatorUserId);
                eval.ReplaceScores(Array.Empty<AcademicCriterionScore>(), passScore: 0m);
                eval.FinalizeEvaluation(DateTimeOffset.UtcNow);
                db.AcademicEvaluations.Add(eval);
            }

            await db.SaveChangesAsync();
        });

        var completed = await admin.PostAsync($"/api/v1/projects/{firstProjectId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var releasedDetail = (await leader.GetFromJsonAsync<Envelope<TeamDto>>($"/api/v1/teams/{teamId}", Json))!.Data;
        Assert.False(releasedDetail.IsLocked);
        Assert.False(releasedDetail.HasActiveProject);
        Assert.Null(releasedDetail.ActiveProjectId);

        var secondApply = await leader.PostAsJsonAsync($"/api/v1/projects/{secondProjectId}/applications", new { teamId }, Json);
        Assert.Equal(HttpStatusCode.Created, secondApply.StatusCode);
    }

    [Fact]
    public async Task Accepted_team_project_is_visible_in_my_projects_and_dashboard_before_commitment_confirm()
    {
        var (company, admin) = ((await CreateVerifiedCompanyAsync()), (await CreateAdminClientAsync()));
        var projectId = await PublishProjectAsync(company, admin, $"Pre-Confirm MyProjects {Guid.NewGuid():N}");
        var goalProjectId = await PublishProjectAsync(company, admin, $"Other Goal {Guid.NewGuid():N}");

        var leader = await RegisterStudentAsync("myproj-leader");
        var member = await RegisterStudentAsync("myproj-member");
        var unselected = await RegisterStudentAsync("myproj-unselected");

        var leaderProfileId = await EnsureStudentProfileAsync(leader);
        var memberProfileId = await EnsureStudentProfileAsync(member);
        var unselectedProfileId = await EnsureStudentProfileAsync(unselected);

        Guid teamId = default;
        await factory.RunWithDbContextAsync(async db =>
        {
            var team = new DNTU.SkillBridge.Domain.Teams.Team(
                "Nhóm Chờ Ký B11",
                leaderProfileId,
                teamType: DNTU.SkillBridge.Domain.Teams.TeamType.PROJECT,
                targetProjectId: goalProjectId);
            db.Teams.Add(team);
            db.TeamMembers.Add(new DNTU.SkillBridge.Domain.Teams.TeamMember(team.Id, memberProfileId, DNTU.SkillBridge.Domain.Teams.TeamMemberRole.MEMBER));
            db.TeamMembers.Add(new DNTU.SkillBridge.Domain.Teams.TeamMember(team.Id, unselectedProfileId, DNTU.SkillBridge.Domain.Teams.TeamMemberRole.MEMBER));
            await db.SaveChangesAsync();
            teamId = team.Id;
        });

        // A pending team application for goalProjectId must NOT show up in My Projects
        var goalApplyRes = await leader.PostAsJsonAsync($"/api/v1/projects/{goalProjectId}/applications", new { teamId }, Json);
        Assert.Equal(HttpStatusCode.Created, goalApplyRes.StatusCode);
        var preAcceptProjects = (await leader.GetFromJsonAsync<Envelope<IReadOnlyCollection<WorkspaceProjectSummaryDto>>>("/api/v1/students/me/projects", Json))!.Data;
        Assert.DoesNotContain(preAcceptProjects, p => p.ProjectId == goalProjectId);
        // Withdraw pending goal application before applying to projectId
        var goalAppId = (await goalApplyRes.Content.ReadFromJsonAsync<Envelope<ApplicationIdDto>>(Json))!.Data.Id;
        Assert.Equal(HttpStatusCode.OK, (await leader.DeleteAsync($"/api/v1/applications/{goalAppId}")).StatusCode);

        var applyRes = await leader.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { teamId }, Json);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var appId = (await applyRes.Content.ReadFromJsonAsync<Envelope<ApplicationIdDto>>(Json))!.Data.Id;

        var adjustRes = await company.PostAsJsonAsync($"/api/v1/company/applications/{appId}/request-adjustment", new
        {
            reason = "Chọn 2 thành viên phù hợp vai trò",
            selectedStudentIds = new[] { leaderProfileId, memberProfileId }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await leader.PostAsync($"/api/v1/applications/{appId}/confirm-adjustment", null)).StatusCode);

        var acceptRes = await company.PostAsJsonAsync($"/api/v1/company/applications/{appId}/accept", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);

        // Before confirming B-11 commitment, both accepted team members see the project in My Projects and Student Dashboard
        var leaderProjects = (await leader.GetFromJsonAsync<Envelope<IReadOnlyCollection<WorkspaceProjectSummaryDto>>>("/api/v1/students/me/projects", Json))!.Data;
        var memberProjects = (await member.GetFromJsonAsync<Envelope<IReadOnlyCollection<WorkspaceProjectSummaryDto>>>("/api/v1/students/me/projects", Json))!.Data;
        var unselectedProjects = (await unselected.GetFromJsonAsync<Envelope<IReadOnlyCollection<WorkspaceProjectSummaryDto>>>("/api/v1/students/me/projects", Json))!.Data;

        var leaderRow = Assert.Single(leaderProjects, p => p.ProjectId == projectId);
        Assert.DoesNotContain(leaderProjects, p => p.ProjectId == goalProjectId);
        Assert.Equal(teamId, leaderRow.TeamId);
        Assert.Equal("Nhóm Chờ Ký B11", leaderRow.TeamName);
        Assert.True(leaderRow.MemberCount >= 2);
        Assert.Contains(memberProjects, p => p.ProjectId == projectId);
        Assert.DoesNotContain(unselectedProjects, p => p.ProjectId == projectId);

        var leaderDashboard = (await leader.GetFromJsonAsync<Envelope<StudentDashboardSummaryDto>>("/api/v1/student/dashboard", Json))!.Data;
        Assert.Contains(leaderDashboard.CurrentProjects, p => p.ProjectId == projectId);

        // If a member declines their pending commitment, the project disappears from their list
        var declineRes = await member.PostAsJsonAsync($"/api/v1/projects/{projectId}/commitment/decline", new { reason = "Trùng lịch" }, Json);
        Assert.Equal(HttpStatusCode.OK, declineRes.StatusCode);

        var memberProjectsAfterDecline = (await member.GetFromJsonAsync<Envelope<IReadOnlyCollection<WorkspaceProjectSummaryDto>>>("/api/v1/students/me/projects", Json))!.Data;
        Assert.DoesNotContain(memberProjectsAfterDecline, p => p.ProjectId == projectId);

        var leaderProjectsStill = (await leader.GetFromJsonAsync<Envelope<IReadOnlyCollection<WorkspaceProjectSummaryDto>>>("/api/v1/students/me/projects", Json))!.Data;
        Assert.Contains(leaderProjectsStill, p => p.ProjectId == projectId);
    }

    private async Task<HttpClient> RegisterStudentAsync(string label)
    {
        var email = $"team-{label}-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, displayName = $"Student {label}", studentCode = "22" + Random.Shared.Next(100000, 999999), password = Password, accountType = "Student" }, Json)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        var token = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data.AccessToken;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Test-User-Email", email);
        return client;
    }

    private async Task<Guid> GetUserIdAsync(HttpClient client)
    {
        var email = client.DefaultRequestHeaders.GetValues("X-Test-User-Email").Single();
        Guid id = default;
        await factory.RunWithDbContextAsync(async db => id = await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync());
        return id;
    }

    private async Task<Guid> EnsureStudentProfileAsync(HttpClient studentClient)
    {
        Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync("/api/v1/students/me")).StatusCode);
        var userId = await GetUserIdAsync(studentClient);
        Guid profileId = default;
        await factory.RunWithDbContextAsync(async db => profileId = await db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync());
        return profileId;
    }

    private async Task<HttpClient> CreateVerifiedCompanyAsync()
    {
        var email = $"team-co-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Team Test",
            password = Password,
            accountType = "Company"
        }, Json);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data.AccessToken;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var companyName = $"Công ty Team {Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/companies/me", new { name = companyName }, Json)).StatusCode);
        await factory.RunWithDbContextAsync(async db =>
        {
            var company = await db.Companies.SingleAsync(item => item.Name == companyName);
            company.MarkVerified(DateTimeOffset.UtcNow, null);
            await db.SaveChangesAsync();
        });
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        Guid adminUserId = default;
        await factory.RunWithDbContextAsync(async db =>
        {
            var user = new User($"team-admin-{Guid.NewGuid():N}@skillbridge.local", "Quản trị Team Test", "seed-hash");
            db.Users.Add(user);
            await db.SaveChangesAsync();
            adminUserId = user.Id;
        });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminUserId.ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, RoleNames.Admin)
        };
        foreach (var permission in DNTU.SkillBridge.Infrastructure.Persistence.Seed.IdentitySeed.GetPermissionsForRole(RoleNames.Admin))
        {
            claims.Add(new Claim("permission", permission));
        }

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), credentials);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        return client;
    }

    private async Task<Guid> PublishProjectAsync(HttpClient company, HttpClient admin, string title)
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<Envelope<IReadOnlyCollection<SkillDto>>>("/api/v1/catalog/skills", Json))!.Data;
        var skillId = skills.First().Id;

        var created = await company.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title,
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            expectedStudentCount = 3,
            minTeamSize = 2,
            maxTeamSize = 4,
            skills = new[] { new { skillId, requirementLevel = "MUST_HAVE", isRequired = true } },
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var projectId = (await created.Content.ReadFromJsonAsync<Envelope<ProjectDto>>(Json))!.Data.Id;

        var updated = await company.PutAsJsonAsync($"/api/v1/company/projects/{projectId}", new
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
            minTeamSize = 2,
            maxTeamSize = 4,
            allowanceAmount = 2_000_000,
            allowanceCurrency = "VND",
            skills = new[] { new { skillId, requirementLevel = "MUST_HAVE", isRequired = true } },
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await company.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);
        return projectId;
    }

    private sealed record Envelope<T>(T Data);
    private sealed record AuthDto(string AccessToken);
    private sealed record TeamDto(Guid Id, string Name, string? AvatarUrl, bool IsLocked, Guid? ActiveProjectId, string? ActiveProjectPhase, bool HasActiveProject);
    private sealed record InvitationDto(Guid Id, string Status);
    private sealed record MemberDto(Guid StudentId, string Role);
    private sealed record SkillDto(Guid Id);
    private sealed record ProjectDto(Guid Id);
    private sealed record ApplicationIdDto(Guid Id);
    private sealed record WorkspaceProjectSummaryDto(Guid ProjectId, Guid? TeamId, string? TeamName, int MemberCount);
    private sealed record DashboardProjectItemDto(Guid ProjectId);
    private sealed record StudentDashboardSummaryDto(IReadOnlyCollection<DashboardProjectItemDto> CurrentProjects);
}
