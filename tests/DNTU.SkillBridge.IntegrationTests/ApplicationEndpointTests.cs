using System.IdentityModel.Tokens.Jwt;
using System.Collections.Concurrent;
using DNTU.SkillBridge.Application.Files;
using DNTU.SkillBridge.Application.Commitments;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Meetings;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Workspaces;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DNTU.SkillBridge.IntegrationTests;

/// <summary>
/// Task 16: student application create/detail/list/withdraw. Every seed uses a unique
/// title prefix so assertions stay deterministic across the shared test database.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class ApplicationEndpointTests(CatalogApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = CatalogApiJson.Options;
    private const string Password = "Password#123456";
    private const string SigningKey = "integration-tests-signing-key-with-at-least-sixty-four-characters-0001";

    private readonly string prefix = $"za{Guid.NewGuid():N}"[..10];

    [Fact]
    public async Task Apply_Lists_Detail_AreConsistent()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Visible", deadlineInDays: 14);
        var student = await RegisterStudentAsync();

        var applied = await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications",
            new { coverLetter = "Tôi rất muốn tham gia dự án này." }, Json);
        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);
        var application = (await applied.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal("PENDING", application.Status);
        Assert.Equal("Tôi rất muốn tham gia dự án này.", application.CoverLetter);
        Assert.Equal(projectId, application.ProjectId);
        Assert.Null(application.WithdrawnAt);

        // Appears in the caller's own list.
        var list = (await student.GetFromJsonAsync<PagedDto>("/api/v1/applications/me?page=1&pageSize=10", Json))!;
        Assert.Equal(1, list.Meta.TotalItems);
        Assert.Contains(list.Data, item => item.Id == application.Id);

        // Detail by id matches the creation payload.
        var detail = (await (await student.GetAsync($"/api/v1/applications/{application.Id}"))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(application.Id, detail.Id);
        Assert.Equal("PENDING", detail.Status);
        Assert.False(string.IsNullOrWhiteSpace(detail.ProjectTitle));
        Assert.False(string.IsNullOrWhiteSpace(detail.CompanyName));
    }

    [Fact]
    public async Task Apply_Twice_ToSameProject_Is409()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Dup", deadlineInDays: 14);
        var student = await RegisterStudentAsync();

        Assert.Equal(HttpStatusCode.Created,
            (await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json)).StatusCode);
    }

    [Fact]
    public async Task Apply_ToDraft_Is404_And_PastDeadline_Is409()
    {
        var (company, admin) = await SeedAsync();
        var skillIds = await GetActiveSkillIdsAsync(1);
        var draftId = await CreateProjectAsync(company, $"{prefix} Draft", skillIds);
        var student = await RegisterStudentAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await student.PostAsJsonAsync($"/api/v1/projects/{draftId}/applications", new { }, Json)).StatusCode);

        // A published project whose deadline has already elapsed no longer accepts applications.
        var pastDeadlineId = await PublishProjectAsync(company, admin, $"{prefix} Late", deadlineInDays: 14);
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var project = await dbContext.Projects.SingleAsync(item => item.Id == pastDeadlineId);
            typeof(DNTU.SkillBridge.Domain.Projects.Project)
                .GetProperty(nameof(DNTU.SkillBridge.Domain.Projects.Project.ApplicationDeadline))!
                .SetValue(project, DateTimeOffset.UtcNow.AddDays(-1));
            await dbContext.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Conflict,
            (await student.PostAsJsonAsync($"/api/v1/projects/{pastDeadlineId}/applications", new { }, Json)).StatusCode);
    }

    [Fact]
    public async Task Withdraw_SetsStatus_And_SecondWithdraw_Is409()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Withdraw", deadlineInDays: 14);
        var student = await RegisterStudentAsync();

        var created = (await (await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications",
            new { coverLetter = "will withdraw" }, Json)).Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        var withdrawn = await DeleteWithBodyAsync(student, $"/api/v1/applications/{created.Id}", new { withdrawReason = "Trùng lịch học" });
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        var payload = (await withdrawn.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal("WITHDRAWN", payload.Status);
        Assert.NotNull(payload.WithdrawnAt);

        // Withdrawing again (already WITHDRAWN) answers 409.
        var again = await DeleteWithBodyAsync(student, $"/api/v1/applications/{created.Id}", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task ForeignStudent_Cannot_GetOrWithdraw_OthersApplication()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Foreign", deadlineInDays: 14);
        var owner = await RegisterStudentAsync();
        var outsider = await RegisterStudentAsync();

        var created = (await (await owner.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications",
            new { }, Json)).Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.GetAsync($"/api/v1/applications/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await DeleteWithBodyAsync(outsider, $"/api/v1/applications/{created.Id}", new { })).StatusCode);
    }

    [Fact]
    public async Task CompanyToken_Is403_OnStudentApplicationEndpoints()
    {
        var company = await CreateVerifiedCompanyAsync();
        var projectId = await PublishProjectAsync(company, await CreateAdminClientAsync(), $"{prefix} Guard", deadlineInDays: 14);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await company.GetAsync("/api/v1/applications/me")).StatusCode);
    }

    [Fact]
    public async Task Company_CanReviewOwnApplication_ButCannotReviewForeignCompanyApplication()
    {
        var (company, admin) = await SeedAsync();
        var foreignCompany = await CreateVerifiedCompanyAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Review", deadlineInDays: 14);
        var student = await RegisterStudentAsync();
        var created = (await (await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        var listed = await company.GetAsync("/api/v1/company/applications?status=PENDING");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains($"\"id\":\"{created.Id}\"", await listed.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{created.Id}/shortlist", new { }, Json)).StatusCode);
        var decision = await company.PostAsJsonAsync($"/api/v1/company/applications/{created.Id}/accept", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        Assert.Contains("ACCEPTED", await decision.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK,
            (await student.GetAsync($"/api/v1/projects/{projectId}/commitment")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await student.GetAsync($"/api/v1/workspaces/{projectId}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await student.GetAsync($"/api/v1/workspaces/{projectId}/activity?page=1&pageSize=10&eventType=COMMITMENT_CONFIRMED")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await company.GetAsync($"/api/v1/workspaces/{projectId}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await company.PutAsJsonAsync($"/api/v1/workspaces/{projectId}/settings", new
            {
                membersCanCreateTasks = false,
                membersCanScheduleMeetings = true,
                workingAgreement = "Cập nhật hằng ngày trước 09:00 UTC."
            }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await foreignCompany.GetAsync($"/api/v1/workspaces/{projectId}/overview")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await foreignCompany.GetAsync($"/api/v1/company/applications/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Rejected_or_withdrawn_application_cannot_be_accepted()
    {
        var (company, admin) = await SeedAsync();
        var rejectedProjectId = await PublishProjectAsync(company, admin, $"{prefix} Rejected", deadlineInDays: 14);
        var withdrawnProjectId = await PublishProjectAsync(company, admin, $"{prefix} Withdrawn", deadlineInDays: 14);
        var student = await RegisterStudentAsync();

        var rejectedApplication = (await (await student.PostAsJsonAsync($"/api/v1/projects/{rejectedProjectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        var withdrawnApplication = (await (await student.PostAsJsonAsync($"/api/v1/projects/{withdrawnProjectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{rejectedApplication.Id}/reject", new { reason = "Thiếu kỹ năng bắt buộc." }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{rejectedApplication.Id}/accept", new { }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await DeleteWithBodyAsync(student, $"/api/v1/applications/{withdrawnApplication.Id}", new { withdrawReason = "Thay đổi kế hoạch." })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{withdrawnApplication.Id}/accept", new { }, Json)).StatusCode);
    }

    [Fact]
    public async Task Accept_NominatedTeam_LocksTeam_AndCreatesCommitmentsForAllMembers()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Team", deadlineInDays: 14);
        var leader = await RegisterStudentAsync();
        var member = await RegisterStudentAsync();
        var leaderStudentId = await EnsureStudentProfileAsync(leader);
        var memberStudentId = await EnsureStudentProfileAsync(member);
        Guid teamId = default;

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var team = new DNTU.SkillBridge.Domain.Teams.Team("Nhóm được chọn", leaderStudentId);
            dbContext.Teams.Add(team);
            dbContext.TeamMembers.Add(new DNTU.SkillBridge.Domain.Teams.TeamMember(team.Id, memberStudentId, DNTU.SkillBridge.Domain.Teams.TeamMemberRole.MEMBER));
            await dbContext.SaveChangesAsync();
            teamId = team.Id;
        });

        var applied = await leader.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { teamId }, Json);
        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);
        var application = (await applied.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)).StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var team = await dbContext.Teams.SingleAsync(item => item.Id == teamId);
            Assert.True(team.IsLocked);
            Assert.Equal(2, await dbContext.ProjectCommitments.CountAsync(item => item.ProjectId == projectId));
        });
    }

    [Fact]
    public async Task Accept_NominatedTeam_RejectsCrossProjectTeamMemberConflict()
    {
        var (company, admin) = await SeedAsync();
        var firstProjectId = await PublishProjectAsync(company, admin, $"{prefix} First team conflict", deadlineInDays: 14);
        var secondProjectId = await PublishProjectAsync(company, admin, $"{prefix} Second team conflict", deadlineInDays: 14);
        var firstLeader = await RegisterStudentAsync();
        var secondLeader = await RegisterStudentAsync();
        var sharedMember = await RegisterStudentAsync();
        var firstLeaderStudentId = await EnsureStudentProfileAsync(firstLeader);
        var secondLeaderStudentId = await EnsureStudentProfileAsync(secondLeader);
        var sharedStudentId = await EnsureStudentProfileAsync(sharedMember);
        Guid firstTeamId = default;
        Guid secondTeamId = default;

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var firstTeam = new DNTU.SkillBridge.Domain.Teams.Team("Nhóm đã được nhận", firstLeaderStudentId);
            var secondTeam = new DNTU.SkillBridge.Domain.Teams.Team("Nhóm xung đột", secondLeaderStudentId);
            dbContext.Teams.AddRange(firstTeam, secondTeam);
            dbContext.TeamMembers.Add(new DNTU.SkillBridge.Domain.Teams.TeamMember(firstTeam.Id, sharedStudentId, DNTU.SkillBridge.Domain.Teams.TeamMemberRole.MEMBER));
            dbContext.TeamMembers.Add(new DNTU.SkillBridge.Domain.Teams.TeamMember(secondTeam.Id, sharedStudentId, DNTU.SkillBridge.Domain.Teams.TeamMemberRole.MEMBER));
            await dbContext.SaveChangesAsync();
            firstTeamId = firstTeam.Id;
            secondTeamId = secondTeam.Id;
        });

        var firstApplication = (await (await firstLeader.PostAsJsonAsync($"/api/v1/projects/{firstProjectId}/applications", new { teamId = firstTeamId }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{firstApplication.Id}/accept", new { }, Json)).StatusCode);

        var secondApplication = (await (await secondLeader.PostAsJsonAsync($"/api/v1/projects/{secondProjectId}/applications", new { teamId = secondTeamId }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.Conflict,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{secondApplication.Id}/accept", new { }, Json)).StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.Equal(2, await dbContext.ProjectCommitments.CountAsync(item => item.ProjectId == firstProjectId));
            Assert.Equal(0, await dbContext.ProjectCommitments.CountAsync(item => item.ProjectId == secondProjectId));
            Assert.False(await dbContext.ProjectActivities.AnyAsync(item => item.ProjectId == secondProjectId && item.EventType == "APPLICATION_ACCEPTED"));
        });
    }

    [Fact]
    public async Task ConcurrentAccepts_RespectProjectCapacity()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Capacity", deadlineInDays: 14);
        var students = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegisterStudentAsync()));

        var applications = await Task.WhenAll(students.Select(async student =>
        {
            var response = await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        }));

        var decisions = await Task.WhenAll(applications.Select(application =>
            company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)));

        Assert.Equal(3, decisions.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, decisions.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        var accepted = await company.GetFromJsonAsync<PagedDto>(
            $"/api/v1/company/applications?projectId={projectId}&status=ACCEPTED", Json);
        Assert.NotNull(accepted);
        Assert.Equal(3, accepted.Meta.TotalItems);
    }

    [Fact]
    public async Task Withdrawal_Workflow_EnforcesLecturerScope_AdminDecision_AndExpiryProcessing()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Governed withdrawal", deadlineInDays: 14);
        var student = await RegisterStudentAsync();
        var application = (await (await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);

        var created = await student.PostAsJsonAsync("/api/v1/withdrawals", new
        {
            projectId,
            reason = "Không thể tiếp tục vì thay đổi lịch học."
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var withdrawal = (await created.Content.ReadFromJsonAsync<Envelope<WithdrawalDto>>(Json))!.Data;

        var (assignedLecturer, lecturerId) = await CreateLecturerClientAsync();
        var otherLecturer = (await CreateLecturerClientAsync()).Client;
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/v1/lecturers/{lecturerId}/assignments", new
        {
            projectId,
            role = "SUPERVISOR"
        }, Json)).StatusCode);
        var assignments = await assignedLecturer.GetFromJsonAsync<ListEnvelope<LecturerAssignmentDto>>("/api/v1/lecturers/me/assignments", Json);
        Assert.NotNull(assignments);
        Assert.Equal(HttpStatusCode.OK, (await assignedLecturer.PutAsync($"/api/v1/lecturers/me/assignments/{assignments!.Data.Single().Id}/accept", null)).StatusCode);

        // Task 20 scopes: only the assigned lecturer and an administrator can access this workspace.
        Assert.Equal(HttpStatusCode.OK, (await assignedLecturer.GetAsync($"/api/v1/workspaces/{projectId}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/workspaces/{projectId}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherLecturer.GetAsync($"/api/v1/workspaces/{projectId}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await student.PostAsync($"/api/v1/workspaces/{projectId}/activity", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await otherLecturer.PostAsJsonAsync($"/api/v1/withdrawals/{withdrawal.Id}/recommend", new { note = "Không thuộc dự án." }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await assignedLecturer.PostAsJsonAsync($"/api/v1/withdrawals/{withdrawal.Id}/recommend", new { note = "Đề nghị phê duyệt." }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/withdrawals/{withdrawal.Id}/approve", new { note = "Đã phê duyệt." }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/withdrawals/{withdrawal.Id}/reject", new { note = "Không thể thay đổi quyết định." }, Json)).StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var persistedWithdrawal = await dbContext.WithdrawalRequests.SingleAsync(item => item.Id == withdrawal.Id);
            Assert.Equal(WithdrawalStatus.APPROVED, persistedWithdrawal.Status);
            Assert.False(await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.StudentId == persistedWithdrawal.StudentId && member.IsActive));
        });
        var myProjectsAfterWithdrawal = await student.GetFromJsonAsync<ListEnvelope<WorkspaceOverviewDto>>("/api/v1/students/me/projects", Json);
        Assert.NotNull(myProjectsAfterWithdrawal);
        Assert.DoesNotContain(myProjectsAfterWithdrawal!.Data, item => item.ProjectId == projectId);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/api/v1/students/me/projects/{projectId}")).StatusCode);

        var expiryProjectId = await PublishProjectAsync(company, admin, $"{prefix} Expiry", deadlineInDays: 14);
        var blockedProjectId = await PublishProjectAsync(company, admin, $"{prefix} Blocked after expiry", deadlineInDays: 14);
        var expiryStudent = await RegisterStudentAsync();
        var expiryApplication = (await (await expiryStudent.PostAsJsonAsync($"/api/v1/projects/{expiryProjectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync($"/api/v1/company/applications/{expiryApplication.Id}/accept", new { }, Json)).StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var commitment = await dbContext.ProjectCommitments.SingleAsync(item => item.ProjectId == expiryProjectId);
            typeof(DNTU.SkillBridge.Domain.Commitments.ProjectCommitment)
                .GetProperty(nameof(DNTU.SkillBridge.Domain.Commitments.ProjectCommitment.ConfirmationDeadline))!
                .SetValue(commitment, DateTimeOffset.UtcNow.AddMinutes(-1));
            await dbContext.SaveChangesAsync();

            var expired = await new CommitmentService(new DNTU.SkillBridge.Infrastructure.Repositories.CommitmentRepository(dbContext), new DNTU.SkillBridge.Infrastructure.Persistence.UnitOfWork(dbContext)).ExpirePendingCommitmentsAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.True(expired >= 1);
            Assert.Equal(DNTU.SkillBridge.Domain.Commitments.CommitmentStatus.ABANDONED,
                await dbContext.ProjectCommitments.Where(item => item.ProjectId == expiryProjectId).Select(item => item.Status).SingleAsync());
            Assert.True(await dbContext.ProjectActivities.AnyAsync(item => item.ProjectId == expiryProjectId && item.EventType == "COMMITMENT_ABANDONED"));
        });
        Assert.Equal(HttpStatusCode.Conflict,
            (await expiryStudent.PostAsJsonAsync($"/api/v1/projects/{blockedProjectId}/applications", new { }, Json)).StatusCode);
    }

    [Fact]
    public async Task WorkspaceActivity_IsPagedAndFilteredByEventType()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Activity filters", deadlineInDays: 14);
        var student = await RegisterStudentAsync();
        var application = (await (await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/shortlist", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);

        var firstPage = await student.GetFromJsonAsync<ActivityPagedDto>($"/api/v1/workspaces/{projectId}/activity?page=1&pageSize=1", Json);
        var secondPage = await student.GetFromJsonAsync<ActivityPagedDto>($"/api/v1/workspaces/{projectId}/activity?page=2&pageSize=1", Json);
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Equal(1, firstPage!.Meta.Page);
        Assert.Equal(1, firstPage.Meta.PageSize);
        Assert.True(firstPage.Meta.TotalItems >= 3);
        Assert.Single(firstPage.Data);
        Assert.Single(secondPage!.Data);
        Assert.NotEqual(firstPage.Data.Single().Id, secondPage.Data.Single().Id);

        var confirmedOnly = await student.GetFromJsonAsync<ActivityPagedDto>(
            $"/api/v1/workspaces/{projectId}/activity?page=1&pageSize=10&eventType=COMMITMENT_CONFIRMED", Json);
        Assert.NotNull(confirmedOnly);
        Assert.Equal(1, confirmedOnly!.Meta.TotalItems);
        Assert.All(confirmedOnly.Data, item => Assert.Equal("COMMITMENT_CONFIRMED", item.EventType));
    }

    [Fact]
    public async Task Kanban_TaskCrud_EnforcesProjectScope_OptimisticConcurrency_AndBoardColumns()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Kanban", deadlineInDays: 14);
        var member = await RegisterStudentAsync();
        var application = (await (await member.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);

        var created = await member.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Xây dựng API Kanban",
            description = "Cần hoàn tất endpoint và kiểm thử.",
            priority = "HIGH"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<Envelope<ProjectTaskDto>>(Json))!.Data;

        var board = await member.GetAsync($"/api/v1/projects/{projectId}/board");
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        using var boardJson = JsonDocument.Parse(await board.Content.ReadAsStringAsync());
        Assert.Equal(5, boardJson.RootElement.GetProperty("columns").EnumerateObject().Count());

        var moved = await member.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new
        {
            status = "IN_PROGRESS",
            sortOrder = 0,
            version = task.Version
        }, Json);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var movedTask = (await moved.Content.ReadFromJsonAsync<Envelope<ProjectTaskDto>>(Json))!.Data;
        Assert.NotEqual(task.Version, movedTask.Version);
        var staleUpdate = await member.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", new
        {
            title = "Phiên bản lỗi thời",
            priority = "HIGH",
            version = task.Version
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var persisted = await dbContext.ProjectTasks.SingleAsync(item => item.Id == task.Id);
            Assert.Equal(ProjectTaskStatus.IN_PROGRESS, persisted.Status);
            Assert.Equal(movedTask.Version, persisted.Version);
            Assert.Equal(1, await dbContext.ProjectActivities.CountAsync(item => item.ProjectId == projectId && item.EventType == "TASK_MOVED"));
            Assert.Equal(0, await dbContext.ProjectActivities.CountAsync(item => item.ProjectId == projectId && item.EventType == "TASK_UPDATED"));
        });

        var outsider = await RegisterStudentAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/projects/{projectId}/board")).StatusCode);
    }

    [Fact]
    public async Task TaskCollaboration_MyTasks_AndLecturerTasks_AreScopedAndVersioned()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Task scope", deadlineInDays: 14);
        var foreignProjectId = await PublishProjectAsync(company, admin, $"{prefix} Foreign task scope", deadlineInDays: 14);
        var member = await RegisterStudentAsync();
        var memberStudentId = await EnsureStudentProfileAsync(member);
        var otherStudent = await RegisterStudentAsync();
        var otherStudentId = await EnsureStudentProfileAsync(otherStudent);
        await AcceptAndConfirmAsync(company, member, projectId);
        await AcceptAndConfirmAsync(company, otherStudent, foreignProjectId);

        var created = await member.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Checklist scoped task",
            priority = "HIGH",
            dueAt = DateTimeOffset.UtcNow.AddDays(3)
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<Envelope<ProjectTaskDto>>(Json))!.Data;
        var assigned = await member.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/assign", new { studentId = memberStudentId, version = task.Version }, Json);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        task = (await assigned.Content.ReadFromJsonAsync<Envelope<ProjectTaskDto>>(Json))!.Data;

        var invalidAssignee = await member.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/assign", new { studentId = otherStudentId, version = task.Version }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalidAssignee.StatusCode);

        var comment = await member.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/comments", new { content = "Đã bắt đầu kiểm thử." }, Json);
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherStudent.GetAsync($"/api/v1/tasks/{task.Id}/comments")).StatusCode);

        var checklist = await member.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/checklist-items", new { title = "Viết kiểm thử phạm vi" }, Json);
        Assert.Equal(HttpStatusCode.Created, checklist.StatusCode);
        var item = (await checklist.Content.ReadFromJsonAsync<Envelope<ChecklistItemDto>>(Json))!.Data;
        var updated = await member.PatchAsJsonAsync($"/api/v1/checklist-items/{item.Id}", new { title = "Viết kiểm thử phạm vi", isCompleted = true, version = task.Version }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var stale = await member.PatchAsJsonAsync($"/api/v1/checklist-items/{item.Id}", new { title = "Không được ghi đè", isCompleted = false, version = task.Version }, Json);
        Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);

        var myTasks = await member.GetFromJsonAsync<PagedProjectTasksDto>("/api/v1/students/me/tasks?assignedOnly=true&priority=HIGH", Json);
        Assert.NotNull(myTasks);
        Assert.Contains(myTasks!.Data, row => row.Id == task.Id);
        Assert.DoesNotContain(myTasks.Data, row => row.ProjectId == foreignProjectId);

        var (lecturer, lecturerId) = await CreateLecturerClientAsync();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/v1/lecturers/{lecturerId}/assignments", new { projectId, role = "SUPERVISOR" }, Json)).StatusCode);
        var assignments = await lecturer.GetFromJsonAsync<ListEnvelope<LecturerAssignmentDto>>("/api/v1/lecturers/me/assignments", Json);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PutAsync($"/api/v1/lecturers/me/assignments/{assignments!.Data.Single().Id}/accept", null)).StatusCode);
        var lecturerTasks = await lecturer.GetFromJsonAsync<PagedProjectTasksDto>("/api/v1/lecturer/tasks?assignedOnly=true", Json);
        Assert.NotNull(lecturerTasks);
        Assert.Contains(lecturerTasks!.Data, row => row.Id == task.Id);
        Assert.DoesNotContain(lecturerTasks.Data, row => row.ProjectId == foreignProjectId);
    }

    [Fact]
    public async Task Files_RejectCrossScopeDeleteAndCompletionTampering_WithFakeStorage()
    {
        var storage = new FakeFileStorage();
        await using var storageFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:Enabled", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFileStorage>();
                services.AddSingleton<IFileStorage>(storage);
            });
        });

        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Files", deadlineInDays: 14);
        var member = await RegisterStudentAsync();
        var outsider = await RegisterStudentAsync();
        await AcceptAndConfirmAsync(company, member, projectId);
        var memberFileClient = storageFactory.CreateClient();
        memberFileClient.DefaultRequestHeaders.Authorization = member.DefaultRequestHeaders.Authorization;
        var outsiderFileClient = storageFactory.CreateClient();
        outsiderFileClient.DefaultRequestHeaders.Authorization = outsider.DefaultRequestHeaders.Authorization;

        var checksum = new string('a', 64);
        var upload = await memberFileClient.PostAsJsonAsync("/api/v1/files/upload-requests", new
        {
            fileName = "evidence.pdf",
            contentType = "application/pdf",
            sizeBytes = 8,
            category = "submission",
            projectId
        }, Json);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var file = (await upload.Content.ReadFromJsonAsync<Envelope<FileUploadResponseDto>>(Json))!.Data.File;

        var storageKey = await ReadStorageKeyAsync(file.Id);
        storage.Put(storageKey, new StoredObjectMetadata(8, checksum, "application/pdf", FileSignature.Pdf));

        Assert.Equal(HttpStatusCode.NotFound, (await outsiderFileClient.GetAsync($"/api/v1/files/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsiderFileClient.GetAsync($"/api/v1/files/{file.Id}/download-url")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsiderFileClient.DeleteAsync($"/api/v1/files/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await outsiderFileClient.PostAsJsonAsync($"/api/v1/files/{file.Id}/complete", new { checksumSha256 = checksum }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await memberFileClient.PostAsJsonAsync($"/api/v1/files/{file.Id}/complete", new { checksumSha256 = new string('b', 64) }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await memberFileClient.PostAsJsonAsync($"/api/v1/files/{file.Id}/complete", new { checksumSha256 = checksum }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await memberFileClient.GetAsync($"/api/v1/files/{file.Id}/download-url")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await memberFileClient.DeleteAsync($"/api/v1/files/{file.Id}")).StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.Equal(FileUploadStatus.DELETED, await dbContext.FileRecords.Where(row => row.Id == file.Id).Select(row => row.Status).SingleAsync());
        });
    }

    [Fact]
    public async Task List_StatusFilter_SeparatesPendingAndWithdrawn()
    {
        var (company, admin) = await SeedAsync();
        var pendingProject = await PublishProjectAsync(company, admin, $"{prefix} F Pending", deadlineInDays: 14);
        var withdrawnProject = await PublishProjectAsync(company, admin, $"{prefix} F Withdrawn", deadlineInDays: 14);
        var student = await RegisterStudentAsync();

        var pending = (await (await student.PostAsJsonAsync($"/api/v1/projects/{pendingProject}/applications",
            new { }, Json)).Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        var toWithdraw = (await (await student.PostAsJsonAsync($"/api/v1/projects/{withdrawnProject}/applications",
            new { }, Json)).Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        await DeleteWithBodyAsync(student, $"/api/v1/applications/{toWithdraw.Id}", new { });

        var pendingList = (await student.GetFromJsonAsync<PagedDto>("/api/v1/applications/me?status=PENDING", Json))!;
        Assert.Equal(1, pendingList.Meta.TotalItems);
        Assert.Equal(pending.Id, pendingList.Data.Single().Id);

        var withdrawnList = (await student.GetFromJsonAsync<PagedDto>("/api/v1/applications/me?status=WITHDRAWN", Json))!;
        Assert.Equal(1, withdrawnList.Meta.TotalItems);
        Assert.Equal(toWithdraw.Id, withdrawnList.Data.Single().Id);
    }

    [Fact]
    public async Task Meetings_EnforceScope_UtcApprovedLinks_AttendancePolicy_AndReminderOutbox()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Meetings", deadlineInDays: 14);
        var foreignProjectId = await PublishProjectAsync(company, admin, $"{prefix} Foreign meetings", deadlineInDays: 14);
        var participant = await RegisterStudentAsync();
        var participantStudentId = await EnsureStudentProfileAsync(participant);
        var outsider = await RegisterStudentAsync();
        var outsiderStudentId = await EnsureStudentProfileAsync(outsider);
        var application = (await (await participant.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await participant.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);

        var startAt = DateTimeOffset.UtcNow.AddDays(2);
        var invalidAttendee = await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/meetings", new
        {
            title = "Họp phạm vi",
            startAt,
            endAt = startAt.AddHours(1),
            externalUrl = "https://meet.google.com/abc-defg-hij",
            participantStudentIds = new[] { outsiderStudentId }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalidAttendee.StatusCode);

        var nonUtc = await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/meetings", new
        {
            title = "Họp sai múi giờ",
            startAt = new DateTimeOffset(2026, 9, 5, 9, 0, 0, TimeSpan.FromHours(7)),
            endAt = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(7)),
            participantStudentIds = new[] { participantStudentId }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, nonUtc.StatusCode);

        var created = await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/meetings", new
        {
            title = "Họp sprint",
            description = "Rà soát tiến độ.",
            startAt,
            endAt = startAt.AddHours(1),
            externalUrl = "https://teams.microsoft.com/l/meetup-join/example",
            participantStudentIds = new[] { participantStudentId }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var meeting = (await created.Content.ReadFromJsonAsync<Envelope<MeetingDto>>(Json))!.Data;
        Assert.Equal("MICROSOFT_TEAMS", meeting.ExternalLinkType);
        Assert.Equal("SCHEDULED", meeting.ReminderState);

        Assert.Equal(HttpStatusCode.OK, (await participant.GetAsync("/api/v1/students/me/meetings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/projects/{projectId}/meetings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PutAsJsonAsync($"/api/v1/meetings/{meeting.Id}/attendance", new { studentId = participantStudentId, attendanceStatus = "ATTENDED" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await company.PutAsJsonAsync($"/api/v1/meetings/{meeting.Id}/attendance", new { studentId = participantStudentId, attendanceStatus = "ATTENDED" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/meetings/{meeting.Id}/participants")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync($"/api/v1/projects/{foreignProjectId}/meetings")).StatusCode);
        var companyMeetings = await company.GetFromJsonAsync<ListEnvelope<MeetingDto>>("/api/v1/company/meetings", Json);
        Assert.NotNull(companyMeetings);
        Assert.Contains(companyMeetings!.Data, row => row.Id == meeting.Id);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.Contains(await dbContext.OutboxMessages.Select(item => item.Type).ToListAsync(), type => type == "meeting.reminder.schedule");
            Assert.True(await dbContext.ProjectActivities.AnyAsync(item => item.ProjectId == projectId && item.EventType == "MEETING_ATTENDANCE_UPDATED"));
            Assert.Contains(dbContext.Model.FindEntityType(typeof(DNTU.SkillBridge.Domain.Meetings.ProjectMeeting))!.GetIndexes(), index =>
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { "ProjectId", "StartAt" }));
        });
    }

    [Fact]
    public async Task MeetingMinutes_ActionItems_EnforceScope_AreAudited_AndConvertIdempotently()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Minutes", deadlineInDays: 14);
        var participant = await RegisterStudentAsync();
        var participantStudentId = await EnsureStudentProfileAsync(participant);
        var outsider = await RegisterStudentAsync();
        var application = (await (await participant.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await participant.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);

        var startAt = DateTimeOffset.UtcNow.AddDays(2);
        var createdMeeting = await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/meetings", new
        {
            title = "Họp quyết định sprint",
            startAt,
            endAt = startAt.AddHours(1),
            participantStudentIds = new[] { participantStudentId }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createdMeeting.StatusCode);
        var meeting = (await createdMeeting.Content.ReadFromJsonAsync<Envelope<MeetingDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/meetings/{meeting.Id}/minutes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PutAsJsonAsync($"/api/v1/meetings/{meeting.Id}/minutes", new { content = "Quyết định không được phép sửa." }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PutAsJsonAsync($"/api/v1/meetings/{meeting.Id}/minutes", new { content = "Quyết định A." }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PutAsJsonAsync($"/api/v1/meetings/{meeting.Id}/minutes", new { content = "Quyết định B." }, Json)).StatusCode);

        var createItem = await company.PostAsJsonAsync($"/api/v1/meetings/{meeting.Id}/action-items", new
        {
            description = "Hoàn thiện API minutes",
            responsibleStudentId = participantStudentId,
            dueAt = DateTimeOffset.UtcNow.AddDays(5)
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createItem.StatusCode);
        var actionItem = (await createItem.Content.ReadFromJsonAsync<Envelope<MeetingActionItemDto>>(Json))!.Data;

        var invalidDueDate = await company.PostAsJsonAsync($"/api/v1/meetings/{meeting.Id}/action-items", new
        {
            description = "Mốc hẹn sai",
            responsibleStudentId = participantStudentId,
            dueAt = meeting.EndAt.AddMinutes(-1)
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDueDate.StatusCode);

        var completed = await company.PutAsJsonAsync($"/api/v1/meeting-action-items/{actionItem.Id}", new
        {
            description = actionItem.Description,
            responsibleStudentId = participantStudentId,
            dueAt = DateTimeOffset.UtcNow.AddDays(6),
            isCompleted = true,
            version = actionItem.Version
        }, Json);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var completedActionItem = (await completed.Content.ReadFromJsonAsync<Envelope<MeetingActionItemDto>>(Json))!.Data;
        Assert.True(completedActionItem.IsCompleted);
        Assert.NotEqual(actionItem.Version, completedActionItem.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await company.PutAsJsonAsync($"/api/v1/meeting-action-items/{actionItem.Id}", new
        {
            description = actionItem.Description,
            responsibleStudentId = participantStudentId,
            dueAt = DateTimeOffset.UtcNow.AddDays(7),
            isCompleted = false,
            version = actionItem.Version
        }, Json)).StatusCode);

        var conversions = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => company.PostAsJsonAsync($"/api/v1/meeting-action-items/{actionItem.Id}/convert-to-task", new { priority = "HIGH" }, Json)));
        Assert.All(conversions, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var converted = await conversions[0].Content.ReadFromJsonAsync<Envelope<MeetingActionItemConversionDto>>(Json);
        Assert.NotNull(converted);
        Assert.Equal(projectId, converted!.Data.Task.ProjectId);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var persistedActionItem = await dbContext.MeetingActionItems.SingleAsync(item => item.Id == actionItem.Id);
            Assert.Equal(converted.Data.Task.Id, persistedActionItem.ProjectTaskId);
            Assert.Equal(1, await dbContext.ProjectTasks.CountAsync(task => task.Id == persistedActionItem.ProjectTaskId));
            Assert.Equal(1, await dbContext.MeetingMinuteRevisions.CountAsync(revision => revision.MeetingMinuteId == dbContext.MeetingMinutes.Where(minute => minute.MeetingId == meeting.Id).Select(minute => minute.Id).Single()));
            Assert.True(await dbContext.ProjectActivities.AnyAsync(activity => activity.ProjectId == projectId && activity.EventType == "MEETING_ACTION_ITEM_CONVERTED"));
            Assert.True(await dbContext.OutboxMessages.AnyAsync(message => message.Type == "meeting.action-item.changed"));
        });
    }

    [Fact]
    public async Task Milestones_RejectForeignFiles_AndConcurrentApprovalHasSingleWinner()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Milestone full", deadlineInDays: 14);
        var foreignProjectId = await PublishProjectAsync(company, admin, $"{prefix} Milestone foreign", deadlineInDays: 14);
        var member = await RegisterStudentAsync();
        await AcceptAndConfirmAsync(company, member, projectId);
        var memberUserId = GetUserId(member);

        var foreignFileId = await SeedCompletedFileAsync(memberUserId, foreignProjectId, "foreign.pdf");
        var localFileId = await SeedCompletedFileAsync(memberUserId, projectId, "local.pdf");
        var created = await company.PostAsJsonAsync($"/api/v1/projects/{projectId}/milestones", new
        {
            title = "Bàn giao kiểm thử",
            sequence = 1,
            dueAt = DateTimeOffset.UtcNow.AddDays(10)
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var milestone = (await created.Content.ReadFromJsonAsync<Envelope<MilestoneDto>>(Json))!.Data;

        Assert.Equal(HttpStatusCode.Conflict, (await member.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/deliverables", new
        {
            name = "Sai dự án",
            criteria = "Không nhận file ngoài scope",
            fileId = foreignFileId
        }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await member.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/deliverables", new
        {
            name = "Bằng chứng đúng scope",
            criteria = "File đã hoàn tất và thuộc dự án",
            fileId = localFileId
        }, Json)).StatusCode);

        milestone = (await (await member.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/start", new { version = milestone.Version }, Json)).Content.ReadFromJsonAsync<Envelope<MilestoneDto>>(Json))!.Data;
        milestone = (await (await member.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/submit", new { version = milestone.Version }, Json)).Content.ReadFromJsonAsync<Envelope<MilestoneDto>>(Json))!.Data;
        var approvals = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            company.PostAsJsonAsync($"/api/v1/milestones/{milestone.Id}/approve", new { version = milestone.Version, note = "Duyệt một lần." }, Json)));
        Assert.Equal(1, approvals.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, approvals.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            Assert.Equal(MilestoneStatus.APPROVED, await dbContext.ProjectMilestones.Where(row => row.Id == milestone.Id).Select(row => row.Status).SingleAsync());
            Assert.Equal(3, await dbContext.MilestoneApprovalHistories.CountAsync(row => row.MilestoneId == milestone.Id));
        });
    }

    [Fact]
    public async Task Dashboards_UseScopedAggregateProgressAndRiskEvidence()
    {
        var (company, admin) = await SeedAsync();
        var projectId = await PublishProjectAsync(company, admin, $"{prefix} Dashboard", deadlineInDays: 14);
        var foreignProjectId = await PublishProjectAsync(company, admin, $"{prefix} Dashboard foreign", deadlineInDays: 14);
        var member = await RegisterStudentAsync();
        await AcceptAndConfirmAsync(company, member, projectId);
        var memberStudentId = await EnsureStudentProfileAsync(member);
        var foreignStudent = await RegisterStudentAsync();
        await AcceptAndConfirmAsync(company, foreignStudent, foreignProjectId);
        var foreignStudentId = await EnsureStudentProfileAsync(foreignStudent);
        var (lecturer, lecturerId) = await CreateLecturerClientAsync();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/v1/lecturers/{lecturerId}/assignments", new { projectId, role = "SUPERVISOR" }, Json)).StatusCode);
        var assignments = await lecturer.GetFromJsonAsync<ListEnvelope<LecturerAssignmentDto>>("/api/v1/lecturers/me/assignments", Json);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PutAsync($"/api/v1/lecturers/me/assignments/{assignments!.Data.Single().Id}/accept", null)).StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var oldTask = new ProjectTask(projectId, "Overdue task", null, ProjectTaskPriority.HIGH, DateTimeOffset.UtcNow.AddDays(-5), 0);
            oldTask.Assign(memberStudentId, oldTask.Version);
            dbContext.ProjectTasks.Add(oldTask);
            dbContext.ProjectTasks.Add(new ProjectTask(foreignProjectId, "Foreign task", null, ProjectTaskPriority.HIGH, DateTimeOffset.UtcNow.AddDays(-5), 0));
            var overdueMilestone = new ProjectMilestone(projectId, "Overdue milestone", null, 1, DateTimeOffset.UtcNow.AddDays(5));
            typeof(ProjectMilestone).GetProperty(nameof(ProjectMilestone.DueAt))!.SetValue(overdueMilestone, DateTimeOffset.UtcNow.AddDays(-1));
            dbContext.ProjectMilestones.Add(overdueMilestone);
            var meeting = new ProjectMeeting(projectId, "Missed meeting", null, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1), null);
            typeof(ProjectMeeting).GetProperty(nameof(ProjectMeeting.StartAt))!.SetValue(meeting, DateTimeOffset.UtcNow.AddDays(-1));
            typeof(ProjectMeeting).GetProperty(nameof(ProjectMeeting.EndAt))!.SetValue(meeting, DateTimeOffset.UtcNow.AddDays(-1).AddHours(1));
            dbContext.ProjectMeetings.Add(meeting);
            var participant = new MeetingParticipant(meeting.Id, memberStudentId);
            participant.SetAttendance(MeetingAttendanceStatus.ABSENT);
            dbContext.MeetingParticipants.Add(participant);
            var foreignMeeting = new ProjectMeeting(foreignProjectId, "Foreign missed meeting", null, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1), null);
            dbContext.ProjectMeetings.Add(foreignMeeting);
            var foreignParticipant = new MeetingParticipant(foreignMeeting.Id, foreignStudentId);
            foreignParticipant.SetAttendance(MeetingAttendanceStatus.ABSENT);
            dbContext.MeetingParticipants.Add(foreignParticipant);
            await dbContext.SaveChangesAsync();
        });

        var progressResponse = await member.GetAsync($"/api/v1/projects/{projectId}/progress");
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        Assert.Contains("no-store", progressResponse.Headers.CacheControl?.ToString());
        var progress = (await progressResponse.Content.ReadFromJsonAsync<Envelope<ProjectProgressDto>>(Json))!.Data;
        Assert.Equal(projectId, progress.ProjectId);
        Assert.True(progress.TotalTasks >= 1);
        Assert.True(progress.OverdueMilestones >= 1);
        Assert.Equal("HIGH", progress.Risk.Level);
        Assert.Contains(progress.Risk.Reasons, reason => reason.Contains("overdue milestone", StringComparison.OrdinalIgnoreCase));

        var studentDashboard = (await (await member.GetAsync("/api/v1/student/dashboard")).Content.ReadFromJsonAsync<Envelope<StudentDashboardDto>>(Json))!.Data;
        Assert.Contains(studentDashboard.CurrentProjects, row => row.ProjectId == projectId);
        Assert.DoesNotContain(studentDashboard.CurrentProjects, row => row.ProjectId == foreignProjectId);
        var lecturerDashboard = (await (await lecturer.GetAsync("/api/v1/lecturer/dashboard")).Content.ReadFromJsonAsync<Envelope<LecturerDashboardDto>>(Json))!.Data;
        Assert.Contains(lecturerDashboard.ProjectsAtRisk, row => row.ProjectId == projectId);
        Assert.DoesNotContain(lecturerDashboard.ProjectsAtRisk, row => row.ProjectId == foreignProjectId);
    }

    // ---------- helpers ----------

    private async Task AcceptAndConfirmAsync(HttpClient company, HttpClient student, Guid projectId)
    {
        var application = (await (await student.PostAsJsonAsync($"/api/v1/projects/{projectId}/applications", new { }, Json))
            .Content.ReadFromJsonAsync<Envelope<ApplicationDto>>(Json))!.Data;
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync($"/api/v1/company/applications/{application.Id}/accept", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/projects/{projectId}/commitment/confirm", null)).StatusCode);
    }

    private async Task<string> ReadStorageKeyAsync(Guid fileId)
    {
        await using var dbContext = factory.CreateDbContext();
        return await dbContext.FileRecords.Where(row => row.Id == fileId).Select(row => row.StorageKey).SingleAsync();
    }

    private async Task<Guid> SeedCompletedFileAsync(Guid userId, Guid projectId, string fileName)
    {
        var file = new FileRecord(userId, projectId, fileName, $"tests/{Guid.NewGuid():N}.pdf", "application/pdf", 8, DateTimeOffset.UtcNow.AddHours(1));
        file.Complete(new string('c', 64), DateTimeOffset.UtcNow);
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            dbContext.FileRecords.Add(file);
            await dbContext.SaveChangesAsync();
        });
        return file.Id;
    }

    private async Task<Guid> EnsureStudentProfileAsync(HttpClient client)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/students/me")).StatusCode);
        var userId = GetUserId(client);
        await using var dbContext = factory.CreateDbContext();
        return await dbContext.StudentProfiles.Where(profile => profile.UserId == userId)
            .Select(profile => profile.Id)
            .SingleAsync();
    }

    private static Guid GetUserId(HttpClient client)
    {
        var token = client.DefaultRequestHeaders.Authorization?.Parameter;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Single(claim => claim.Type == ClaimTypes.NameIdentifier).Value);
    }

    private static Task<HttpResponseMessage> DeleteWithBodyAsync(HttpClient client, string url, object payload) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent(payload) });

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private async Task<(HttpClient Company, HttpClient Admin)> SeedAsync()
    {
        var admin = await CreateAdminClientAsync();
        var company = await CreateVerifiedCompanyAsync();
        return (company, admin);
    }

    private async Task<Guid> PublishProjectAsync(HttpClient company, HttpClient admin, string title, int deadlineInDays)
    {
        var skillIds = await GetActiveSkillIdsAsync(1);
        var projectId = await CreateProjectAsync(company, title, skillIds);
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
            applicationDeadline = DateTimeOffset.UtcNow.AddDays(deadlineInDays),
            expectedStudentCount = 3,
            minTeamSize = 2,
            maxTeamSize = 4,
            allowanceAmount = 2_000_000,
            allowanceCurrency = "VND",
            skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await company.PostAsync($"/api/v1/company/projects/{projectId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{projectId}/approve", new { }, Json)).StatusCode);
        return projectId;
    }

    private async Task<List<Guid>> GetActiveSkillIdsAsync(int count)
    {
        var anonymous = factory.CreateClient();
        var skills = (await anonymous.GetFromJsonAsync<ListEnvelope<CatalogSkillDto>>("/api/v1/catalog/skills", Json))!.Data;
        Assert.True(skills.Count >= count, $"Need at least {count} active skills for application tests.");
        return skills.Take(count).Select(skill => skill.Id).ToList();
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client, string title, IReadOnlyList<Guid> skillIds)
    {
        var created = await client.PostAsJsonAsync("/api/v1/company/projects", new
        {
            title,
            difficulty = "BEGINNER",
            workType = "ONSITE",
            durationWeeks = 4,
            expectedStudentCount = 3,
            minTeamSize = 2,
            maxTeamSize = 4,
            skills = skillIds.Select(skillId => new { skillId, requirementLevel = "MUST_HAVE", isRequired = true }).ToArray(),
            deliverables = new[] { new { name = "Báo cáo bàn giao", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Envelope<ProjectDetailDto>>(Json))!.Data.Id;
    }

    private async Task<HttpClient> CreateVerifiedCompanyAsync()
    {
        var client = await RegisterCompanyAsync();
        var companyName = $"Công ty Ứng tuyển {Guid.NewGuid():N}";
        var created = await client.PutAsJsonAsync("/api/v1/companies/me", new { name = companyName }, Json);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var company = await dbContext.Companies.SingleAsync(item => item.Name == companyName);
            company.MarkVerified(DateTimeOffset.UtcNow, null);
            await dbContext.SaveChangesAsync();
        });
        return client;
    }

    private async Task<HttpClient> RegisterCompanyAsync()
    {
        var email = $"apply-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Công ty Ứng tuyển",
            password = Password,
            accountType = "Company"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var companyName = $"Công ty Ứng tuyển {Guid.NewGuid():N}";
        await client.PutAsJsonAsync("/api/v1/companies/me", new { name = companyName }, Json);
        return client;
    }

    private async Task<HttpClient> RegisterStudentAsync()
    {
        var email = $"student-{Guid.NewGuid():N}@skillbridge.local";
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            displayName = "Sinh viên Ứng tuyển",
            password = Password,
            accountType = "Student"
        }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<Envelope<AuthDto>>(Json))!.Data;
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        return client;
    }

    private static string CreateRoleToken(string role, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("session_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("DNTU.SkillBridge.Api", "DNTU.SkillBridge.Web", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<(HttpClient Client, Guid LecturerProfileId)> CreateLecturerClientAsync()
    {
        Guid lecturerUserId = default;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new User($"lecturer-{Guid.NewGuid():N}@skillbridge.local", "Giảng viên kiểm thử", "seed-hash");
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

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        Guid adminUserId = default;
        await factory.RunWithDbContextAsync(async dbContext =>
        {
            var user = new User($"admin-{Guid.NewGuid():N}@skillbridge.local", "Quản trị viên", "seed-hash");
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            adminUserId = user.Id;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateRoleToken(RoleNames.Admin, adminUserId));
        return client;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record ListEnvelope<T>(IReadOnlyCollection<T> Data);

    private sealed record AuthDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, Guid SessionId);

    private sealed record CatalogSkillDto(Guid Id, string Code, string Name, string Category, string? Description, bool IsActive);

    private sealed record ProjectDetailDto(Guid Id, Guid CompanyId, string Code, string Title, string Slug, string Status, bool IsActive);

    private sealed record ApplicationDto(
        Guid Id, Guid ProjectId, string ProjectTitle, string ProjectSlug, string CompanyName,
        string Status, string? CoverLetter, DateTimeOffset CreatedAt, DateTimeOffset? WithdrawnAt);

    private sealed record MetaDto(int Page, int PageSize, int TotalItems, int TotalPages);

    private sealed record PagedDto(IReadOnlyCollection<ApplicationDto> Data, MetaDto Meta);

    private sealed record WithdrawalDto(Guid Id, Guid ProjectId, Guid StudentId, string Reason, string Status);

    private sealed record WorkspaceOverviewDto(Guid ProjectId, string Title, string Slug, string Status);

    private sealed record ActivityDto(Guid Id, string EventType, DateTimeOffset CreatedAt);

    private sealed record ActivityPagedDto(IReadOnlyCollection<ActivityDto> Data, MetaDto Meta);

    private sealed record ProjectTaskDto(Guid Id, Guid ProjectId, string Title, string? Description, string Status, string Priority, Guid? AssigneeStudentId, DateTimeOffset? DueAt, int SortOrder, Guid Version, DateTimeOffset CreatedAt);
    private sealed record ChecklistItemDto(Guid Id, Guid ProjectTaskId, string Title, bool IsCompleted, int SortOrder);
    private sealed record PagedProjectTasksDto(IReadOnlyCollection<ProjectTaskDto> Data, MetaDto Meta);
    private sealed record FileDto(Guid Id, Guid? ProjectId, string FileName, string ContentType, long ExpectedSizeBytes, string Status, DateTimeOffset ExpiresAt, DateTimeOffset? CompletedAt);
    private sealed record FileUploadResponseDto(FileDto File, Uri UploadUrl);

    private sealed record MeetingDto(Guid Id, Guid ProjectId, string Title, string? Description, DateTimeOffset StartAt, DateTimeOffset EndAt, string? ExternalUrl, string ExternalLinkType, string ReminderState, DateTimeOffset CreatedAt);
    private sealed record MeetingActionItemDto(Guid Id, Guid MeetingId, string Description, Guid? ResponsibleStudentId, DateTimeOffset? DueAt, bool IsCompleted, Guid? ProjectTaskId, Guid Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
    private sealed record MeetingActionItemConversionDto(MeetingActionItemDto ActionItem, ProjectTaskDto Task, bool AlreadyConverted);

    private sealed record LecturerProfileDto(Guid Id);

    private sealed record LecturerAssignmentDto(Guid Id, Guid LecturerId, Guid ProjectId, string Role, string Status);

    private sealed record ProjectRiskDto(int Score, string Level, IReadOnlyCollection<string> Reasons, DateTimeOffset CalculatedAt);
    private sealed record MilestoneDto(Guid Id, Guid Version);
    private sealed record ProjectProgressDto(Guid ProjectId, int TotalTasks, int CompletedTasks, int OverdueTasks, int TotalMilestones, int ApprovedMilestones, int OverdueMilestones, decimal CompletionPercent, ProjectRiskDto Risk);
    private sealed record DashboardProjectDto(Guid ProjectId, string Title, string Status, ProjectProgressDto Progress);
    private sealed record StudentDashboardDto(IReadOnlyCollection<DashboardProjectDto> CurrentProjects, int AssignedTasks, int UpcomingMeetings, int RevisionSubmissions, int UnreadNotifications);
    private sealed record LecturerDashboardDto(IReadOnlyCollection<DashboardProjectDto> ProjectsAtRisk, int OverdueMilestones, int SubmissionsWaiting, int UpcomingMeetings, int PendingWithdrawal, int RecentActivity);

    private sealed class FakeFileStorage : IFileStorage
    {
        private readonly ConcurrentDictionary<string, StoredObjectMetadata> objects = new();

        public void Put(string storageKey, StoredObjectMetadata metadata) => objects[storageKey] = metadata;
        public Task<Uri> CreateUploadUrlAsync(string storageKey, string contentType, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromResult(new Uri($"https://storage.test/upload/{Uri.EscapeDataString(storageKey)}"));
        public Task<StoredObjectMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(objects.TryGetValue(storageKey, out var metadata) ? metadata : null);
        public Task<Uri> CreateDownloadUrlAsync(string storageKey, TimeSpan lifetime, CancellationToken cancellationToken) => Task.FromResult(new Uri($"https://storage.test/download/{Uri.EscapeDataString(storageKey)}"));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            objects.TryRemove(storageKey, out _);
            return Task.CompletedTask;
        }
    }
}
