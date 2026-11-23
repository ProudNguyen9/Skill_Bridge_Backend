using System.Data;
using System.Text.Json;
using DNTU.SkillBridge.Application.Applications;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Applications;
using DNTU.SkillBridge.Domain.Common;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Teams;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class ApplicationRepository(
    AppDbContext dbContext,
    INotificationWriter? notificationWriter = null) : IApplicationRepository
{
    private static readonly ProjectStatus[] ApplyableStatuses =
    [
        ProjectStatus.APPROVED,
        ProjectStatus.RECRUITING,
        ProjectStatus.IN_PROGRESS
    ];

    private static readonly ApplicationStatus[] ActiveConflictStatuses =
    [
        ApplicationStatus.PENDING,
        ApplicationStatus.WAITING_MEMBER_CONSENT,
        ApplicationStatus.SHORTLISTED,
        ApplicationStatus.ADJUSTMENT_REQUESTED,
        ApplicationStatus.WAITING_LEADER_APPROVAL
    ];

    public async Task<(ApplicationApplyOutcome Outcome, ApplicationResponse? Application)> ApplyToProjectAsync(
        Guid userId, Guid projectId, CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationApplyOutcome.StudentNotFound, null);

        var project = await dbContext.Projects.AsTracking()
            .SingleOrDefaultAsync(p => p.Id == projectId && p.IsActive, cancellationToken);
        if (project is null || !ApplyableStatuses.Contains(project.Status))
            return (ApplicationApplyOutcome.ProjectNotFound, null);
        if (project.ApplicationDeadline.HasValue && project.ApplicationDeadline.Value <= DateTimeOffset.UtcNow)
            return (ApplicationApplyOutcome.ProjectNotAccepting, null);

        var hasAbandonedCommitment = await dbContext.ProjectCommitments.AsNoTracking()
            .AnyAsync(c => c.StudentId == studentId.Value &&
                           (c.Status == CommitmentStatus.ABANDONED || c.Status == CommitmentStatus.EXPIRED),
                cancellationToken);
        if (hasAbandonedCommitment) return (ApplicationApplyOutcome.ProjectNotAccepting, null);

        Team? nominatedTeam = null;
        List<(Guid StudentId, string? ProposedRole)> selectedMembers = [];

        if (request.TeamId is Guid teamId)
        {
            nominatedTeam = await dbContext.Teams.AsTracking()
                .Include(item => item.Members)
                .SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
            if (nominatedTeam is null || !nominatedTeam.Members.Any(m => m.StudentId == studentId.Value && m.Role == TeamMemberRole.LEADER))
            {
                return (ApplicationApplyOutcome.ProjectNotAccepting, null);
            }

            if (nominatedTeam.IsLocked)
            {
                var hasUnfinishedTarget = nominatedTeam.TargetProjectId.HasValue && await dbContext.Projects.AsNoTracking()
                    .AnyAsync(p => p.Id == nominatedTeam.TargetProjectId.Value &&
                                   p.Status != ProjectStatus.COMPLETED &&
                                   p.Status != ProjectStatus.CANCELLED,
                        cancellationToken);
                var hasUnfinishedAcceptedApp = await dbContext.Applications.AsNoTracking()
                    .AnyAsync(a => a.TeamId == teamId &&
                                   a.Status == ApplicationStatus.ACCEPTED &&
                                   a.Project.Status != ProjectStatus.COMPLETED &&
                                   a.Project.Status != ProjectStatus.CANCELLED,
                        cancellationToken);
                if (hasUnfinishedTarget || hasUnfinishedAcceptedApp)
                {
                    return (ApplicationApplyOutcome.ProjectNotAccepting, null);
                }
                nominatedTeam.ReleaseFromCompletedProject();
            }

            var teamHasOtherActiveApp = await dbContext.Applications.AsNoTracking()
                .AnyAsync(a => a.TeamId == teamId &&
                               a.ProjectId != projectId &&
                               (a.Status == ApplicationStatus.ACCEPTED || ActiveConflictStatuses.Contains(a.Status)) &&
                               a.Project.Status != ProjectStatus.COMPLETED &&
                               a.Project.Status != ProjectStatus.CANCELLED,
                    cancellationToken);
            if (teamHasOtherActiveApp)
            {
                return (ApplicationApplyOutcome.AlreadyApplied, null);
            }

            if (request.Members is { Count: > 0 })
            {
                var teamMemberIds = nominatedTeam.Members.Select(m => m.StudentId).ToHashSet();
                foreach (var item in request.Members.GroupBy(m => m.StudentId).Select(g => g.First()))
                {
                    if (!teamMemberIds.Contains(item.StudentId))
                        return (ApplicationApplyOutcome.ProjectNotAccepting, null);
                    selectedMembers.Add((item.StudentId, item.ProposedRole));
                }
                if (!selectedMembers.Any(m => m.StudentId == studentId.Value))
                {
                    var leaderMember = nominatedTeam.Members.First(m => m.StudentId == studentId.Value);
                    selectedMembers.Insert(0, (studentId.Value, leaderMember.FunctionalRole ?? "Trưởng nhóm"));
                }
            }
            else
            {
                selectedMembers = nominatedTeam.Members
                    .Select(m => (m.StudentId, (string?)(m.FunctionalRole ?? (m.Role == TeamMemberRole.LEADER ? "Trưởng nhóm" : "Thành viên"))))
                    .ToList();
            }

            if (selectedMembers.Count < project.MinTeamSize || selectedMembers.Count > project.MaxTeamSize)
            {
                return (ApplicationApplyOutcome.ProjectNotAccepting, null);
            }
        }

        var application = new Domain.Applications.Application(project.Id, studentId.Value, request.CoverLetter, request.TeamId);
        application.SetAttachments(request.GithubUrl, request.PortfolioUrl, request.CvUrl, request.CvFileName, request.Availability);
        if (request.RecommendedByLecturerId.HasValue)
        {
            application.SetRecommendedByLecturer(request.RecommendedByLecturerId.Value);
        }

        var needsMemberConsent = nominatedTeam is not null &&
                                 request.RequireMemberConsent &&
                                 selectedMembers.Any(m => m.StudentId != studentId.Value);
        if (needsMemberConsent)
        {
            application.StartMemberConsent();
        }

        foreach (var (memberStudentId, proposedRole) in selectedMembers)
        {
            var consent = !needsMemberConsent || memberStudentId == studentId.Value
                ? MemberConsentStatus.APPROVED
                : MemberConsentStatus.PENDING;
            application.Members.Add(new ApplicationMember(application.Id, memberStudentId, proposedRole, consent));
        }

        if (nominatedTeam is not null)
        {
            nominatedTeam.SetTargetProject(project.Id);
        }

        dbContext.Applications.Add(application);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (ApplicationApplyOutcome.AlreadyApplied, null);
        }

        if (needsMemberConsent)
        {
            var memberStudentIds = selectedMembers.Where(m => m.StudentId != studentId.Value).Select(m => m.StudentId).ToList();
            var memberUserIds = await dbContext.StudentProfiles.AsNoTracking()
                .Where(s => memberStudentIds.Contains(s.Id))
                .Select(s => s.UserId)
                .ToListAsync(cancellationToken);
            foreach (var memberUserId in memberUserIds)
            {
                notificationWriter?.Add(new NotificationMessage(
                    memberUserId,
                    "TEAM_APPLICATION_CONSENT_REQUESTED",
                    $"Nhóm \"{nominatedTeam!.Name}\" mời bạn xác nhận hồ sơ ứng tuyển \"{project.Title}\"",
                    JsonSerializer.Serialize(new
                    {
                        summary = $"Nhóm trưởng mời bạn xác nhận đồng ý chia sẻ hồ sơ ứng tuyển bài toán {project.Title}.",
                        url = "/student/applications",
                        applicationId = application.Id,
                        projectId = project.Id,
                        teamId = nominatedTeam.Id,
                    })));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await NotifyCompanyAsync(project.Id, project.CompanyId, "application.created",
                nominatedTeam is not null
                    ? $"Nhóm \"{nominatedTeam.Name}\" đã nộp đơn ứng tuyển bài toán \"{project.Title}\""
                    : $"Hồ sơ ứng tuyển mới cho đề tài {project.Title}",
                new
                {
                    summary = "Một ứng viên hoặc nhóm vừa nộp hồ sơ ứng tuyển vào đề tài của bạn. Vào mục Hồ sơ ứng tuyển để duyệt.",
                    url = $"/company/projects/{project.Id}/applications",
                    applicationId = application.Id,
                    projectId = project.Id,
                    applicationType = application.ApplicationType.ToString(),
                },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (ApplicationApplyOutcome.Applied, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<ApplicationResponse?> GetMyApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await ProjectApplicationAsync(applicationId, studentId.Value, cancellationToken);
    }

    public async Task<PagedResponse<ApplicationResponse>> ListMyApplicationsAsync(Guid userId, ApplicationListQuery query, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return new PagedResponse<ApplicationResponse>([], PageMetadata.Create(query.Page, query.PageSize, 0));

        var baseQuery = dbContext.Applications.AsNoTracking().Where(a => a.StudentId == studentId.Value);
        if (Enum.TryParse<ApplicationStatus>(query.Status?.Trim(), ignoreCase: true, out var status))
        {
            baseQuery = baseQuery.Where(a => a.Status == status);
        }

        var totalItems = await baseQuery.CountAsync(cancellationToken);
        var ids = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var items = await ProjectApplicationsByIdsAsync(ids, cancellationToken);
        return new PagedResponse<ApplicationResponse>(items, PageMetadata.Create(query.Page, query.PageSize, totalItems));
    }

    public async Task<IReadOnlyCollection<ApplicationResponse>> ListMyGroupMemberApplicationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];

        var ids = await dbContext.ApplicationMembers.AsNoTracking()
            .Where(am => am.StudentId == studentId.Value && am.Application.StudentId != studentId.Value)
            .OrderByDescending(am => am.Application.CreatedAt)
            .Select(am => am.ApplicationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await ProjectApplicationsByIdsAsync(ids, cancellationToken);
    }

    public async Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> RespondMemberConsentAsync(
        Guid userId, Guid applicationId, bool approve, string? note, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationActionOutcome.Forbidden, null);

        var application = await dbContext.Applications.AsTracking()
            .Include(a => a.Members)
            .Include(a => a.Project)
            .SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);
        if (application is null) return (ApplicationActionOutcome.NotFound, null);
        if (application.Status != ApplicationStatus.WAITING_MEMBER_CONSENT)
            return (ApplicationActionOutcome.Conflict, null);

        var member = application.Members.SingleOrDefault(m => m.StudentId == studentId.Value);
        if (member is null) return (ApplicationActionOutcome.Forbidden, null);

        var now = DateTimeOffset.UtcNow;
        if (approve) member.ApproveConsent(now, note);
        else member.DeclineConsent(now, note);

        var leaderUserId = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => s.Id == application.StudentId)
            .Select(s => (Guid?)s.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (leaderUserId.HasValue && leaderUserId.Value != userId)
        {
            notificationWriter?.Add(new NotificationMessage(
                leaderUserId.Value,
                approve ? "TEAM_APPLICATION_CONSENT_APPROVED" : "TEAM_APPLICATION_CONSENT_DECLINED",
                approve
                    ? $"Thành viên đã đồng ý chia sẻ hồ sơ cho đơn ứng tuyển \"{application.Project.Title}\""
                    : $"Thành viên đã từ chối tham gia đơn ứng tuyển \"{application.Project.Title}\"",
                JsonSerializer.Serialize(new { applicationId = application.Id, projectId = application.ProjectId, approved = approve })));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (ApplicationActionOutcome.Success, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> SubmitTeamApplicationAsync(
        Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationActionOutcome.Forbidden, null);

        var application = await dbContext.Applications.AsTracking()
            .Include(a => a.Members)
            .Include(a => a.Project)
            .Include(a => a.Team)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.StudentId == studentId.Value, cancellationToken);
        if (application is null) return (ApplicationActionOutcome.NotFound, null);
        if (application.Status != ApplicationStatus.WAITING_MEMBER_CONSENT)
            return (ApplicationActionOutcome.Conflict, null);

        if (application.Members.Any(m => m.ConsentStatus != MemberConsentStatus.APPROVED))
        {
            return (ApplicationActionOutcome.Conflict, null);
        }

        application.SubmitAfterMemberConsent();

        await NotifyCompanyAsync(application.ProjectId, application.Project.CompanyId, "APPLICATION_RECEIVED",
            $"Nhóm \"{application.Team?.Name ?? "Sinh viên"}\" đã nộp đơn ứng tuyển bài toán \"{application.Project.Title}\"",
            new { applicationId = application.Id, projectId = application.ProjectId, applicationType = "TEAM" },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return (ApplicationActionOutcome.Success, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> ConfirmAdjustmentAsync(
        Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationActionOutcome.Forbidden, null);

        var application = await dbContext.Applications.AsTracking()
            .Include(a => a.Project)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.StudentId == studentId.Value, cancellationToken);
        if (application is null) return (ApplicationActionOutcome.NotFound, null);
        if (application.Status != ApplicationStatus.ADJUSTMENT_REQUESTED)
            return (ApplicationActionOutcome.Conflict, null);

        application.ConfirmAdjustment();

        await NotifyCompanyAsync(application.ProjectId, application.Project.CompanyId, "APPLICATION_ADJUSTMENT_CONFIRMED",
            $"Trưởng nhóm đã xác nhận đề xuất điều chỉnh cho bài toán \"{application.Project.Title}\"",
            new { applicationId = application.Id, projectId = application.ProjectId },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return (ApplicationActionOutcome.Success, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<IReadOnlyCollection<ApplicationConflictItemResponse>> CheckConflictsForProjectAsync(
        Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];

        var directApps = await dbContext.Applications.AsNoTracking()
            .Where(a => a.ProjectId == projectId &&
                        a.StudentId == studentId.Value &&
                        ActiveConflictStatuses.Contains(a.Status))
            .Select(a => new ApplicationConflictItemResponse(
                a.Id,
                a.ProjectId,
                a.Project.Title,
                a.ApplicationType.ToString(),
                a.Status.ToString(),
                a.ApplicationType == ApplicationType.TEAM ? "Trưởng nhóm" : "Cá nhân",
                a.IsPreferredInConflict,
                a.PriorityOrder))
            .ToListAsync(cancellationToken);

        var teamMemberApps = await dbContext.ApplicationMembers.AsNoTracking()
            .Where(am => am.StudentId == studentId.Value &&
                         am.Application.ProjectId == projectId &&
                         am.Application.StudentId != studentId.Value &&
                         ActiveConflictStatuses.Contains(am.Application.Status))
            .Select(am => new ApplicationConflictItemResponse(
                am.ApplicationId,
                am.Application.ProjectId,
                am.Application.Project.Title,
                "TEAM",
                am.Application.Status.ToString(),
                am.ProposedRole ?? "Thành viên nhóm",
                am.Application.IsPreferredInConflict,
                am.Application.PriorityOrder))
            .ToListAsync(cancellationToken);

        return directApps.Concat(teamMemberApps).GroupBy(x => x.ApplicationId).Select(g => g.First()).ToList();
    }

    public async Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> ResolveConflictAsync(
        Guid userId, Guid applicationId, ResolveApplicationConflictRequest request, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationActionOutcome.Forbidden, null);

        var application = await dbContext.Applications.AsTracking()
            .SingleOrDefaultAsync(a => a.Id == applicationId &&
                                       (a.StudentId == studentId.Value || a.Members.Any(m => m.StudentId == studentId.Value)),
                cancellationToken);
        if (application is null) return (ApplicationActionOutcome.NotFound, null);

        application.SetConflictPreference(request.Preferred, request.PriorityOrder, request.Note);

        if (request.WithdrawOtherApplicationId is Guid otherId && otherId != application.Id)
        {
            var otherApp = await dbContext.Applications.AsTracking()
                .SingleOrDefaultAsync(a => a.Id == otherId && a.StudentId == studentId.Value, cancellationToken);
            if (otherApp is not null && ActiveConflictStatuses.Contains(otherApp.Status))
            {
                otherApp.Withdraw(DateTimeOffset.UtcNow, "Sinh viên chọn ưu tiên đơn khác khi giải quyết xung đột (Bước 18).", userId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (ApplicationActionOutcome.Success, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<(ApplicationWithdrawOutcome Outcome, ApplicationResponse? Application)> WithdrawMyApplicationAsync(
        Guid userId, Guid applicationId, string? reason, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationWithdrawOutcome.NotFound, null);

        var application = await dbContext.Applications.AsTracking()
            .Include(a => a.Members)
            .Include(a => a.Project)
            .SingleOrDefaultAsync(item => item.Id == applicationId && item.StudentId == studentId.Value, cancellationToken);
        if (application is null) return (ApplicationWithdrawOutcome.NotFound, null);

        if (application.Status is not (ApplicationStatus.PENDING or ApplicationStatus.WAITING_MEMBER_CONSENT or ApplicationStatus.SHORTLISTED or ApplicationStatus.ADJUSTMENT_REQUESTED or ApplicationStatus.WAITING_LEADER_APPROVAL))
        {
            return (ApplicationWithdrawOutcome.NotWithdrawable, null);
        }

        application.Withdraw(DateTimeOffset.UtcNow, reason, userId);

        if (application.ApplicationType == ApplicationType.TEAM && application.Members.Count > 0)
        {
            var memberStudentIds = application.Members.Where(m => m.StudentId != studentId.Value).Select(m => m.StudentId).ToList();
            if (memberStudentIds.Count > 0)
            {
                var memberUserIds = await dbContext.StudentProfiles.AsNoTracking()
                    .Where(s => memberStudentIds.Contains(s.Id))
                    .Select(s => s.UserId)
                    .ToListAsync(cancellationToken);
                foreach (var memberUserId in memberUserIds)
                {
                    notificationWriter?.Add(new NotificationMessage(
                        memberUserId,
                        "TEAM_APPLICATION_WITHDRAWN",
                        $"Trưởng nhóm đã rút đơn ứng tuyển bài toán \"{application.Project.Title}\"",
                        JsonSerializer.Serialize(new { applicationId = application.Id, projectId = application.ProjectId, reason })));
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (ApplicationWithdrawOutcome.Withdrawn, await ProjectApplicationAsync(application.Id, studentId.Value, cancellationToken));
    }

    public async Task<PagedResponse<CompanyApplicationResponse>> ListCompanyApplicationsAsync(
        Guid companyId, CompanyApplicationListQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.Applications.AsNoTracking()
            .Where(a => a.Project.CompanyId == companyId &&
                        a.Project.IsActive &&
                        a.Status != ApplicationStatus.WAITING_MEMBER_CONSENT);
        if (query.ProjectId.HasValue) baseQuery = baseQuery.Where(a => a.ProjectId == query.ProjectId.Value);
        if (Enum.TryParse<ApplicationStatus>(query.Status?.Trim(), true, out var status))
            baseQuery = baseQuery.Where(a => a.Status == status);
        if (Enum.TryParse<ApplicationType>(query.ApplicationType?.Trim(), true, out var appType))
            baseQuery = baseQuery.Where(a => a.ApplicationType == appType);

        var total = await baseQuery.CountAsync(cancellationToken);
        var ids = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var items = await ProjectCompanyApplicationsByIdsAsync(ids, cancellationToken);
        return new PagedResponse<CompanyApplicationResponse>(items, PageMetadata.Create(query.Page, query.PageSize, total));
    }

    public async Task<IReadOnlyCollection<CompanyProjectApplicationSummaryResponse>> GetCompanyApplicationSummaryAsync(
        Guid companyId, CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .Select(p => new { p.Id, p.ExpectedStudentCount })
            .ToListAsync(cancellationToken);
        if (projects.Count == 0) return [];

        var projectIds = projects.Select(p => p.Id).ToList();

        var rows = await dbContext.Applications.AsNoTracking()
            .Where(a => projectIds.Contains(a.ProjectId) &&
                        a.Status != ApplicationStatus.WAITING_MEMBER_CONSENT)
            .Select(a => new { a.ProjectId, a.Status, a.ApplicationType, a.TeamId })
            .ToListAsync(cancellationToken);

        var commitmentCounts = await dbContext.ProjectCommitments.AsNoTracking()
            .Where(c => projectIds.Contains(c.ProjectId) &&
                        (c.Status == CommitmentStatus.PENDING || c.Status == CommitmentStatus.CONFIRMED))
            .GroupBy(c => c.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count, cancellationToken);

        // Resolve accepted team per project (via accepted application with TeamId OR locked project team)
        var acceptedTeamIdsByProject = rows
            .Where(r => r.Status == ApplicationStatus.ACCEPTED && r.TeamId.HasValue)
            .GroupBy(r => r.ProjectId)
            .ToDictionary(g => g.Key, g => g.First().TeamId!.Value);

        var lockedTargetTeams = await dbContext.Teams.AsNoTracking()
            .Where(t => t.TargetProjectId.HasValue && projectIds.Contains(t.TargetProjectId.Value) && t.IsLocked)
            .Select(t => new { ProjectId = t.TargetProjectId!.Value, t.Id })
            .ToListAsync(cancellationToken);
        foreach (var lt in lockedTargetTeams)
        {
            acceptedTeamIdsByProject.TryAdd(lt.ProjectId, lt.Id);
        }

        var teamIds = acceptedTeamIdsByProject.Values.Distinct().ToList();
        var teamsById = teamIds.Count == 0
            ? new Dictionary<Guid, (string Name, Guid LeaderStudentId, string LeaderName, string? AvatarUrl)>()
            : (await (
                from t in dbContext.Teams.AsNoTracking()
                where teamIds.Contains(t.Id)
                let leaderMember = dbContext.TeamMembers.AsNoTracking()
                    .Where(m => m.TeamId == t.Id && m.Role == TeamMemberRole.LEADER)
                    .Select(m => (Guid?)m.StudentId)
                    .FirstOrDefault()
                let leaderSid = leaderMember ?? t.CreatedByStudentId
                join sp in dbContext.StudentProfiles.AsNoTracking() on leaderSid equals sp.Id
                join u in dbContext.Users.AsNoTracking() on sp.UserId equals u.Id
                select new { t.Id, t.Name, LeaderStudentId = leaderSid, LeaderName = u.DisplayName, AvatarUrl = (string?)t.AvatarUrl })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.Id, x => (Name: x.Name, LeaderStudentId: x.LeaderStudentId, LeaderName: x.LeaderName, AvatarUrl: (string?)x.AvatarUrl));

        var rowsByProject = rows.GroupBy(r => r.ProjectId).ToDictionary(g => g.Key, g => g.ToList());

        return projects.Select(p =>
        {
            var list = rowsByProject.GetValueOrDefault(p.Id, []);
            Guid? teamId = acceptedTeamIdsByProject.TryGetValue(p.Id, out var tid) ? tid : null;
            (string Name, Guid LeaderStudentId, string LeaderName, string? AvatarUrl) teamInfo = default;
            var hasTeamInfo = teamId.HasValue && teamsById.TryGetValue(teamId.Value, out teamInfo);

            return new CompanyProjectApplicationSummaryResponse(
                p.Id,
                list.Count,
                list.Count(x => x.Status == ApplicationStatus.PENDING),
                list.Count(x => x.Status == ApplicationStatus.SHORTLISTED),
                list.Count(x => x.Status == ApplicationStatus.ADJUSTMENT_REQUESTED),
                list.Count(x => x.Status == ApplicationStatus.ACCEPTED),
                list.Count(x => x.Status == ApplicationStatus.REJECTED),
                list.Count(x => x.ApplicationType == ApplicationType.TEAM),
                list.Count(x => x.ApplicationType == ApplicationType.INDIVIDUAL),
                commitmentCounts.GetValueOrDefault(p.Id, 0),
                p.ExpectedStudentCount,
                teamId,
                hasTeamInfo ? teamInfo.Name : null,
                hasTeamInfo ? teamInfo.LeaderStudentId : null,
                hasTeamInfo ? teamInfo.LeaderName : null,
                list.Count(x => x.Status == ApplicationStatus.WAITING_LEADER_APPROVAL),
                hasTeamInfo ? teamInfo.AvatarUrl : null);
        }).ToList();
    }

    public async Task<CompanyApplicationResponse?> GetCompanyApplicationAsync(
        Guid companyId, Guid applicationId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Applications.AsNoTracking()
            .AnyAsync(a => a.Id == applicationId &&
                           a.Project.CompanyId == companyId &&
                           a.Status != ApplicationStatus.WAITING_MEMBER_CONSENT,
                cancellationToken);
        if (!exists) return null;

        var items = await ProjectCompanyApplicationsByIdsAsync([applicationId], cancellationToken);
        return items.SingleOrDefault();
    }

    public async Task<(ApplicationWithdrawOutcome Outcome, CompanyApplicationResponse? Application)> DecideCompanyApplicationAsync(
        Guid companyId, Guid companyUserId, Guid applicationId, ApplicationStatus targetStatus, ApplicationDecisionRequest? request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var application = await dbContext.Applications.AsTracking()
            .Include(item => item.Project)
            .Include(item => item.Members)
            .Include(item => item.Team).ThenInclude(team => team!.Members)
            .SingleOrDefaultAsync(item => item.Id == applicationId && item.Project.CompanyId == companyId, cancellationToken);
        if (application is null || application.Status == ApplicationStatus.WAITING_MEMBER_CONSENT)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationWithdrawOutcome.NotFound, null);
        }

        var reason = request?.Reason;
        var now = DateTimeOffset.UtcNow;
        List<Guid> notifyStudentIds = [];

        try
        {
            if (targetStatus == ApplicationStatus.ACCEPTED)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM [projects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {application.ProjectId}", cancellationToken);

                Guid[] selectedStudentIds;
                if (application.Members.Count > 0)
                {
                    selectedStudentIds = application.Members
                        .Where(m => m.IsSelectedByCompany && m.ConsentStatus == MemberConsentStatus.APPROVED)
                        .Select(m => m.StudentId)
                        .Distinct()
                        .ToArray();
                    if (selectedStudentIds.Length == 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return (ApplicationWithdrawOutcome.NotWithdrawable, null);
                    }
                }
                else
                {
                    selectedStudentIds = application.Team?.Members.Select(m => m.StudentId).ToArray() ?? [application.StudentId];
                }

                var acceptedStudentCount = await dbContext.ProjectCommitments
                    .CountAsync(c => c.ProjectId == application.ProjectId &&
                                     (c.Status == CommitmentStatus.PENDING || c.Status == CommitmentStatus.CONFIRMED),
                        cancellationToken);
                var waitingLeaderCount = await dbContext.Applications
                    .CountAsync(a => a.ProjectId == application.ProjectId &&
                                     a.Status == ApplicationStatus.WAITING_LEADER_APPROVAL,
                        cancellationToken);

                var existingAcceptedTeam = await ResolveAcceptedProjectTeamAsync(application.ProjectId, cancellationToken);
                if (existingAcceptedTeam is not null)
                {
                    // Dự án đã có nhóm thực hiện: không nhận thêm nhóm thứ 2 hoặc tự ý thêm người lẻ không qua trưởng nhóm.
                    await transaction.RollbackAsync(cancellationToken);
                    return (ApplicationWithdrawOutcome.NotWithdrawable, null);
                }

                if ((application.ApplicationType == ApplicationType.TEAM || application.TeamId.HasValue) &&
                    acceptedStudentCount > 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (ApplicationWithdrawOutcome.NotWithdrawable, null);
                }

                if (acceptedStudentCount + waitingLeaderCount + selectedStudentIds.Length > application.Project.ExpectedStudentCount)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (ApplicationWithdrawOutcome.NotWithdrawable, null);
                }

                var alreadyAcceptedElsewhere = await dbContext.Applications.AnyAsync(
                    item => selectedStudentIds.Contains(item.StudentId) &&
                            item.ProjectId != application.ProjectId &&
                            item.Status == ApplicationStatus.ACCEPTED &&
                            item.Project.Status != ProjectStatus.COMPLETED &&
                            item.Project.Status != ProjectStatus.CANCELLED,
                    cancellationToken);
                var acceptedTeamContainsSelectedStudent = await dbContext.Applications.AnyAsync(
                    item => item.ProjectId != application.ProjectId &&
                            item.Status == ApplicationStatus.ACCEPTED &&
                            item.Project.Status != ProjectStatus.COMPLETED &&
                            item.Project.Status != ProjectStatus.CANCELLED &&
                            item.TeamId.HasValue &&
                            dbContext.TeamMembers.Any(member => member.TeamId == item.TeamId && selectedStudentIds.Contains(member.StudentId)),
                    cancellationToken);
                var alreadyActiveElsewhere = await dbContext.ProjectMembers.AnyAsync(
                    member => selectedStudentIds.Contains(member.StudentId) &&
                              member.ProjectId != application.ProjectId &&
                              member.IsActive &&
                              dbContext.Projects.Any(p => p.Id == member.ProjectId &&
                                                          p.Status != ProjectStatus.COMPLETED &&
                                                          p.Status != ProjectStatus.CANCELLED),
                    cancellationToken);
                if (alreadyAcceptedElsewhere || acceptedTeamContainsSelectedStudent || alreadyActiveElsewhere)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (ApplicationWithdrawOutcome.NotWithdrawable, null);
                }

                application.Accept(companyUserId, now, reason, request?.Conditions, request?.ConfirmationDeadlineDays);
                application.Team?.BindActiveProject(application.ProjectId);

                var deadlineDays = request?.ConfirmationDeadlineDays ?? 7;
                long? perStudentAllowance = application.Project.AllowanceAmount is > 0 && selectedStudentIds.Length > 0
                    ? application.Project.AllowanceAmount.Value / selectedStudentIds.Length
                    : null;

                foreach (var sid in selectedStudentIds)
                {
                    var proposedRole = application.Members.FirstOrDefault(m => m.StudentId == sid)?.ProposedRole
                        ?? (sid == application.StudentId && application.TeamId.HasValue ? "Trưởng nhóm" : "Thành viên");
                    dbContext.ProjectCommitments.Add(new ProjectCommitment(
                        application.ProjectId,
                        sid,
                        now.AddDays(deadlineDays),
                        "v1",
                        application.Id,
                        proposedRole,
                        request?.Conditions,
                        perStudentAllowance));
                }

                notifyStudentIds = selectedStudentIds.ToList();
            }
            else if (targetStatus == ApplicationStatus.SHORTLISTED)
            {
                application.Shortlist(companyUserId, now, reason, request?.InterviewScheduleNote);
                notifyStudentIds = GetApplicationNotifyStudentIds(application);
            }
            else if (targetStatus == ApplicationStatus.REJECTED)
            {
                application.Reject(companyUserId, now, reason);
                notifyStudentIds = GetApplicationNotifyStudentIds(application);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
                return (ApplicationWithdrawOutcome.NotWithdrawable, null);
            }

            if (notifyStudentIds.Count > 0)
            {
                var userIds = await dbContext.StudentProfiles.AsNoTracking()
                    .Where(s => notifyStudentIds.Contains(s.Id))
                    .Select(s => s.UserId)
                    .ToListAsync(cancellationToken);

                var (notifType, notifTitle) = targetStatus switch
                {
                    ApplicationStatus.ACCEPTED => ("APPLICATION_ACCEPTED", $"Chúc mừng! Đơn ứng tuyển \"{application.Project.Title}\" đã được doanh nghiệp chấp nhận"),
                    ApplicationStatus.SHORTLISTED => ("APPLICATION_SHORTLISTED", $"Đơn ứng tuyển \"{application.Project.Title}\" đã được sơ tuyển / mời phỏng vấn"),
                    _ => ("APPLICATION_REJECTED", $"Cập nhật kết quả đơn ứng tuyển \"{application.Project.Title}\"")
                };

                foreach (var uid in userIds)
                {
                    notificationWriter?.Add(new NotificationMessage(
                        uid,
                        notifType,
                        notifTitle,
                        JsonSerializer.Serialize(new
                        {
                            applicationId = application.Id,
                            projectId = application.ProjectId,
                            status = targetStatus.ToString(),
                            reason,
                            interviewScheduleNote = application.InterviewScheduleNote
                        })));
                }
            }

            dbContext.ProjectActivities.Add(new ProjectActivity(application.ProjectId, $"APPLICATION_{targetStatus}", companyUserId));

            // Thông báo kết quả duyệt cho sinh viên (trong cùng transaction).
            var notifiedStudentIds = application.Team?.Members.Select(member => member.StudentId).ToArray() ?? [application.StudentId];
            var studentUserIds = await dbContext.StudentProfiles
                .AsNoTracking()
                .Where(profile => notifiedStudentIds.Contains(profile.Id))
                .Select(profile => profile.UserId)
                .ToListAsync(cancellationToken);
            if (studentUserIds.Count > 0)
            {
                var (notifType, notifTitle, notifSummary) = targetStatus switch
                {
                    ApplicationStatus.ACCEPTED => ("application.accepted",
                        "Chúc mừng! Hồ sơ ứng tuyển của bạn đã được chấp thuận",
                        $"Bạn đã trúng tuyển đề tài {application.Project.Title}. Hãy kiểm tra cam kết và xác nhận trong vòng 7 ngày."),
                    ApplicationStatus.SHORTLISTED => ("application.shortlisted",
                        "Hồ sơ của bạn đã vào vòng phỏng vấn",
                        $"Doanh nghiệp đã chuyển hồ sơ của bạn sang vòng phỏng vấn cho đề tài {application.Project.Title}."),
                    _ => ("application.rejected",
                        "Kết quả hồ sơ ứng tuyển",
                        $"Hồ sơ ứng tuyển đề tài {application.Project.Title} chưa được duyệt lần này."
                        + (string.IsNullOrWhiteSpace(reason) ? "" : $" Phản hồi từ doanh nghiệp: {reason}")),
                };
                var decisionPayload = JsonSerializer.Serialize(new { summary = notifSummary, url = "/student/applications" });
                foreach (var studentUserId in studentUserIds)
                {
                    notificationWriter.Add(new NotificationMessage(studentUserId, notifType, notifTitle, decisionPayload));
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationWithdrawOutcome.NotWithdrawable, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationWithdrawOutcome.NotWithdrawable, null);
        }

        return (ApplicationWithdrawOutcome.Withdrawn, await GetCompanyApplicationAsync(companyId, applicationId, cancellationToken));
    }

    public async Task<(ApplicationActionOutcome Outcome, CompanyApplicationResponse? Application)> RequestAdjustmentAsync(
        Guid companyId, Guid companyUserId, Guid applicationId, RequestAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications.AsTracking()
            .Include(a => a.Project)
            .Include(a => a.Members)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.Project.CompanyId == companyId, cancellationToken);
        if (application is null) return (ApplicationActionOutcome.NotFound, null);
        if (application.Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED))
            return (ApplicationActionOutcome.Conflict, null);

        var selectedSet = request.SelectedStudentIds?.ToHashSet();
        foreach (var member in application.Members)
        {
            var isSelected = selectedSet is null || selectedSet.Contains(member.StudentId);
            string? newRole = null;
            request.MemberRoles?.TryGetValue(member.StudentId, out newRole);
            member.SetCompanySelection(isSelected, newRole);
        }

        var proposalJson = JsonSerializer.Serialize(new
        {
            reason = request.Reason,
            selectedStudentIds = request.SelectedStudentIds,
            memberRoles = request.MemberRoles
        });
        application.RequestAdjustment(companyUserId, DateTimeOffset.UtcNow, request.Reason, proposalJson);

        var notifyStudents = GetApplicationNotifyStudentIds(application);
        var userIds = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => notifyStudents.Contains(s.Id))
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken);
        foreach (var uid in userIds)
        {
            notificationWriter?.Add(new NotificationMessage(
                uid,
                "APPLICATION_ADJUSTMENT_REQUESTED",
                $"Doanh nghiệp đề nghị điều chỉnh hồ sơ nhóm cho bài toán \"{application.Project.Title}\"",
                JsonSerializer.Serialize(new { applicationId = application.Id, projectId = application.ProjectId, reason = request.Reason })));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (ApplicationActionOutcome.Success, await GetCompanyApplicationAsync(companyId, applicationId, cancellationToken));
    }

    public async Task<(ApplicationActionOutcome Outcome, IReadOnlyCollection<CompanyApplicationResponse> Applications)> MergeIndividualApplicationsAsync(
        Guid companyId, Guid companyUserId, Guid projectId, MergeIndividualApplicationsRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var project = await dbContext.Projects.AsTracking()
            .SingleOrDefaultAsync(p => p.Id == projectId && p.CompanyId == companyId && p.IsActive, cancellationToken);
        if (project is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.NotFound, []);
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [projects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {projectId}", cancellationToken);

        var appIds = request.ApplicationIds.Distinct().ToList();
        var apps = await dbContext.Applications.AsTracking()
            .Where(a => appIds.Contains(a.Id) && a.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        if (apps.Count != appIds.Count ||
            apps.Any(a => a.ApplicationType != ApplicationType.INDIVIDUAL || a.Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, []);
        }

        var selectedStudentIds = apps.Select(a => a.StudentId).Distinct().ToArray();
        if (!selectedStudentIds.Contains(request.LeaderStudentId))
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, []);
        }

        var existingAcceptedTeam = await ResolveAcceptedProjectTeamAsync(projectId, cancellationToken);
        if (existingAcceptedTeam is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, []);
        }

        var acceptedCount = await dbContext.ProjectCommitments
            .CountAsync(c => c.ProjectId == projectId &&
                             (c.Status == CommitmentStatus.PENDING || c.Status == CommitmentStatus.CONFIRMED),
                cancellationToken);
        var waitingLeaderCount = await dbContext.Applications
            .CountAsync(a => a.ProjectId == projectId && a.Status == ApplicationStatus.WAITING_LEADER_APPROVAL,
                cancellationToken);
        if (acceptedCount + waitingLeaderCount + selectedStudentIds.Length > project.ExpectedStudentCount)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, []);
        }

        var alreadyActiveElsewhere = await dbContext.ProjectMembers.AnyAsync(
            m => selectedStudentIds.Contains(m.StudentId) &&
                 m.ProjectId != projectId &&
                 m.IsActive &&
                 dbContext.Projects.Any(p => p.Id == m.ProjectId &&
                                             p.Status != ProjectStatus.COMPLETED &&
                                             p.Status != ProjectStatus.CANCELLED),
            cancellationToken);
        var alreadyAcceptedElsewhere = await dbContext.Applications.AnyAsync(
            a => selectedStudentIds.Contains(a.StudentId) &&
                 a.ProjectId != projectId &&
                 a.Status == ApplicationStatus.ACCEPTED &&
                 a.Project.Status != ProjectStatus.COMPLETED &&
                 a.Project.Status != ProjectStatus.CANCELLED,
            cancellationToken);
        if (alreadyActiveElsewhere || alreadyAcceptedElsewhere)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, []);
        }

        var now = DateTimeOffset.UtcNow;
        var mergedTeam = new Team(
            request.TeamName,
            request.LeaderStudentId,
            $"Nhóm ghép từ ứng viên cá nhân cho dự án {project.Title}",
            TeamType.PROJECT,
            projectId,
            request.MemberRoles?.TryGetValue(request.LeaderStudentId, out var leaderRole) == true ? leaderRole : "Trưởng nhóm");
        mergedTeam.Lock();

        foreach (var sid in selectedStudentIds.Where(s => s != request.LeaderStudentId))
        {
            string? role = null;
            request.MemberRoles?.TryGetValue(sid, out role);
            mergedTeam.Members.Add(new TeamMember(mergedTeam.Id, sid, TeamMemberRole.MEMBER, role ?? "Thành viên"));
        }
        dbContext.Teams.Add(mergedTeam);

        long? perStudentAllowance = project.AllowanceAmount is > 0 && selectedStudentIds.Length > 0
            ? project.AllowanceAmount.Value / selectedStudentIds.Length
            : null;

        foreach (var app in apps)
        {
            string? role = null;
            request.MemberRoles?.TryGetValue(app.StudentId, out role);
            var effectiveRole = role ?? (app.StudentId == request.LeaderStudentId ? "Trưởng nhóm" : "Thành viên");
            app.AttachToMergedTeam(mergedTeam.Id);
            app.Accept(companyUserId, now, $"Doanh nghiệp ghép vào nhóm \"{mergedTeam.Name}\" ({effectiveRole}).", request.Conditions, request.ConfirmationDeadlineDays);

            dbContext.ProjectCommitments.Add(new ProjectCommitment(
                projectId,
                app.StudentId,
                now.AddDays(request.ConfirmationDeadlineDays),
                "v1",
                app.Id,
                effectiveRole,
                request.Conditions,
                perStudentAllowance));
        }

        var userIds = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => selectedStudentIds.Contains(s.Id))
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken);
        foreach (var uid in userIds)
        {
            notificationWriter?.Add(new NotificationMessage(
                uid,
                "MERGED_INTO_PROJECT_TEAM",
                $"Bạn đã được tuyển và ghép vào nhóm \"{mergedTeam.Name}\" cho dự án \"{project.Title}\"",
                JsonSerializer.Serialize(new { projectId, teamId = mergedTeam.Id, teamName = mergedTeam.Name })));
        }

        dbContext.ProjectActivities.Add(new ProjectActivity(projectId, "APPLICATIONS_MERGED_TO_TEAM", companyUserId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var projected = await ProjectCompanyApplicationsByIdsAsync(appIds, cancellationToken);
        return (ApplicationActionOutcome.Success, projected);
    }

    public async Task<(ApplicationActionOutcome Outcome, CompanyApplicationResponse? Application)> RequestJoinTeamAsync(
        Guid companyId, Guid companyUserId, Guid applicationId, RequestJoinTeamRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var application = await dbContext.Applications.AsTracking()
            .Include(a => a.Project)
            .Include(a => a.Student)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.Project.CompanyId == companyId, cancellationToken);
        if (application is null || application.Status == ApplicationStatus.WAITING_MEMBER_CONSENT)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.NotFound, null);
        }

        if (application.ApplicationType != ApplicationType.INDIVIDUAL || application.TeamId.HasValue ||
            application.Status is not (ApplicationStatus.PENDING or ApplicationStatus.SHORTLISTED))
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [projects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {application.ProjectId}", cancellationToken);

        var acceptedTeam = await ResolveAcceptedProjectTeamAsync(application.ProjectId, cancellationToken);
        if (acceptedTeam is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        var acceptedStudentCount = await dbContext.ProjectCommitments
            .CountAsync(c => c.ProjectId == application.ProjectId &&
                             (c.Status == CommitmentStatus.PENDING || c.Status == CommitmentStatus.CONFIRMED),
                cancellationToken);
        var waitingLeaderCount = await dbContext.Applications
            .CountAsync(a => a.ProjectId == application.ProjectId &&
                             a.Status == ApplicationStatus.WAITING_LEADER_APPROVAL,
                cancellationToken);
        if (acceptedStudentCount + waitingLeaderCount + 1 > application.Project.ExpectedStudentCount)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        var alreadyAcceptedElsewhere = await dbContext.Applications.AnyAsync(
            item => item.StudentId == application.StudentId &&
                    item.ProjectId != application.ProjectId &&
                    item.Status == ApplicationStatus.ACCEPTED &&
                    item.Project.Status != ProjectStatus.COMPLETED &&
                    item.Project.Status != ProjectStatus.CANCELLED,
            cancellationToken);
        var alreadyActiveElsewhere = await dbContext.ProjectMembers.AnyAsync(
            member => member.StudentId == application.StudentId &&
                      member.ProjectId != application.ProjectId &&
                      member.IsActive &&
                      dbContext.Projects.Any(p => p.Id == member.ProjectId &&
                                                  p.Status != ProjectStatus.COMPLETED &&
                                                  p.Status != ProjectStatus.CANCELLED),
            cancellationToken);
        var alreadyInTeam = await dbContext.TeamMembers.AnyAsync(
            tm => tm.TeamId == acceptedTeam.Value.TeamId && tm.StudentId == application.StudentId,
            cancellationToken);
        if (alreadyAcceptedElsewhere || alreadyActiveElsewhere || alreadyInTeam)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        var now = DateTimeOffset.UtcNow;
        try
        {
            application.RequestJoinTeam(
                acceptedTeam.Value.TeamId,
                companyUserId,
                now,
                request.ProposedRole,
                request.Reason,
                request.Conditions,
                request.ConfirmationDeadlineDays);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        var applicantName = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == application.Student.UserId)
            .Select(u => u.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? DisplayNameFallbacks.Student;

        notificationWriter?.Add(new NotificationMessage(
            acceptedTeam.Value.LeaderUserId,
            "LEADER_JOIN_TEAM_REQUEST",
            $"Doanh nghiệp đề xuất thêm \"{applicantName}\" vào nhóm \"{acceptedTeam.Value.TeamName}\" (dự án \"{application.Project.Title}\")",
            JsonSerializer.Serialize(new
            {
                applicationId = application.Id,
                projectId = application.ProjectId,
                teamId = acceptedTeam.Value.TeamId,
                proposedRole = application.JoinProposedRole,
                url = "/student/applications"
            })));

        notificationWriter?.Add(new NotificationMessage(
            application.Student.UserId,
            "APPLICATION_WAITING_LEADER_APPROVAL",
            $"Doanh nghiệp đã đề xuất ghép bạn vào nhóm \"{acceptedTeam.Value.TeamName}\" — đang chờ nhóm trưởng xác nhận",
            JsonSerializer.Serialize(new
            {
                applicationId = application.Id,
                projectId = application.ProjectId,
                teamId = acceptedTeam.Value.TeamId,
                url = "/student/applications"
            })));

        dbContext.ProjectActivities.Add(new ProjectActivity(application.ProjectId, "APPLICATION_JOIN_TEAM_REQUESTED", companyUserId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (ApplicationActionOutcome.Success, await GetCompanyApplicationAsync(companyId, applicationId, cancellationToken));
    }

    public async Task<IReadOnlyCollection<ApplicationResponse>> ListLeaderJoinRequestsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];

        var leaderTeamIds = await dbContext.Teams.AsNoTracking()
            .Where(t => t.CreatedByStudentId == studentId.Value ||
                        t.Members.Any(m => m.StudentId == studentId.Value && m.Role == TeamMemberRole.LEADER))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (leaderTeamIds.Count == 0) return [];

        var ids = await dbContext.Applications.AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.WAITING_LEADER_APPROVAL &&
                        a.JoinTargetTeamId.HasValue &&
                        leaderTeamIds.Contains(a.JoinTargetTeamId.Value))
            .OrderByDescending(a => a.JoinRequestedAt ?? a.CreatedAt)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        return await ProjectApplicationsByIdsAsync(ids, cancellationToken);
    }

    public async Task<(ApplicationActionOutcome Outcome, ApplicationResponse? Application)> RespondLeaderJoinApprovalAsync(
        Guid userId, Guid applicationId, bool approve, string? note, CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (ApplicationActionOutcome.Forbidden, null);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var application = await dbContext.Applications.AsTracking()
            .Include(a => a.Project)
            .Include(a => a.Student)
            .SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);
        if (application is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.NotFound, null);
        }

        if (application.Status != ApplicationStatus.WAITING_LEADER_APPROVAL || !application.JoinTargetTeamId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        var targetTeam = await dbContext.Teams.AsTracking()
            .Include(t => t.Members)
            .SingleOrDefaultAsync(t => t.Id == application.JoinTargetTeamId.Value, cancellationToken);
        if (targetTeam is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.NotFound, null);
        }

        var isLeader = targetTeam.CreatedByStudentId == studentId.Value ||
                       targetTeam.Members.Any(m => m.StudentId == studentId.Value && m.Role == TeamMemberRole.LEADER);
        if (!isLeader)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Forbidden, null);
        }

        var applicantName = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == application.Student.UserId)
            .Select(u => u.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? DisplayNameFallbacks.Student;

        if (!approve)
        {
            application.DeclineJoinTeam(string.IsNullOrWhiteSpace(note)
                ? $"Trưởng nhóm \"{targetTeam.Name}\" chưa đồng ý ghép thêm thành viên này."
                : note);

            await NotifyCompanyAsync(
                application.ProjectId,
                application.Project.CompanyId,
                "LEADER_JOIN_TEAM_DECLINED",
                $"Trưởng nhóm \"{targetTeam.Name}\" chưa đồng ý thêm \"{applicantName}\" vào dự án \"{application.Project.Title}\"",
                new { applicationId = application.Id, projectId = application.ProjectId, teamId = targetTeam.Id, note },
                cancellationToken);

            notificationWriter?.Add(new NotificationMessage(
                application.Student.UserId,
                "LEADER_JOIN_TEAM_DECLINED",
                $"Đề xuất ghép vào nhóm \"{targetTeam.Name}\" cho dự án \"{application.Project.Title}\" chưa được nhóm trưởng chấp thuận",
                JsonSerializer.Serialize(new { applicationId = application.Id, projectId = application.ProjectId, url = "/student/applications" })));

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (ApplicationActionOutcome.Success, (await ProjectApplicationsByIdsAsync([application.Id], cancellationToken)).SingleOrDefault());
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [projects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {application.ProjectId}", cancellationToken);

        var acceptedStudentCount = await dbContext.ProjectCommitments
            .CountAsync(c => c.ProjectId == application.ProjectId &&
                             (c.Status == CommitmentStatus.PENDING || c.Status == CommitmentStatus.CONFIRMED),
                cancellationToken);
        if (acceptedStudentCount + 1 > application.Project.ExpectedStudentCount)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        var alreadyAcceptedElsewhere = await dbContext.Applications.AnyAsync(
            item => item.StudentId == application.StudentId &&
                    item.ProjectId != application.ProjectId &&
                    item.Status == ApplicationStatus.ACCEPTED &&
                    item.Project.Status != ProjectStatus.COMPLETED &&
                    item.Project.Status != ProjectStatus.CANCELLED,
            cancellationToken);
        var alreadyActiveElsewhere = await dbContext.ProjectMembers.AnyAsync(
            member => member.StudentId == application.StudentId &&
                      member.ProjectId != application.ProjectId &&
                      member.IsActive &&
                      dbContext.Projects.Any(p => p.Id == member.ProjectId &&
                                                  p.Status != ProjectStatus.COMPLETED &&
                                                  p.Status != ProjectStatus.CANCELLED),
            cancellationToken);
        if (alreadyAcceptedElsewhere || alreadyActiveElsewhere)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        var now = DateTimeOffset.UtcNow;
        var proposedRole = application.JoinProposedRole ?? "Thành viên";

        try
        {
            application.ApproveJoinTeam(application.JoinRequestedByUserId ?? userId, now);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        if (!targetTeam.Members.Any(m => m.StudentId == application.StudentId))
        {
            dbContext.TeamMembers.Add(new TeamMember(targetTeam.Id, application.StudentId, TeamMemberRole.MEMBER, proposedRole));
        }

        var deadlineDays = application.ConfirmationDeadlineDays ?? 7;
        var totalStudentsAfterJoin = acceptedStudentCount + 1;
        long? perStudentAllowance = application.Project.AllowanceAmount is > 0 && totalStudentsAfterJoin > 0
            ? application.Project.AllowanceAmount.Value / totalStudentsAfterJoin
            : null;

        dbContext.ProjectCommitments.Add(new ProjectCommitment(
            application.ProjectId,
            application.StudentId,
            now.AddDays(deadlineDays),
            "v1",
            application.Id,
            proposedRole,
            application.CompanyOfferConditions,
            perStudentAllowance));

        notificationWriter?.Add(new NotificationMessage(
            application.Student.UserId,
            "APPLICATION_ACCEPTED",
            $"Bạn đã được nhóm trưởng \"{targetTeam.Name}\" và doanh nghiệp chấp nhận vào dự án \"{application.Project.Title}\"",
            JsonSerializer.Serialize(new
            {
                applicationId = application.Id,
                projectId = application.ProjectId,
                teamId = targetTeam.Id,
                url = "/student/applications"
            })));

        await NotifyCompanyAsync(
            application.ProjectId,
            application.Project.CompanyId,
            "LEADER_JOIN_TEAM_APPROVED",
            $"Trưởng nhóm \"{targetTeam.Name}\" đã đồng ý thêm \"{applicantName}\" vào dự án \"{application.Project.Title}\"",
            new { applicationId = application.Id, projectId = application.ProjectId, teamId = targetTeam.Id },
            cancellationToken);

        dbContext.ProjectActivities.Add(new ProjectActivity(application.ProjectId, "LEADER_APPROVED_JOIN_TEAM", userId));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (ApplicationActionOutcome.Conflict, null);
        }

        return (ApplicationActionOutcome.Success, (await ProjectApplicationsByIdsAsync([application.Id], cancellationToken)).SingleOrDefault());
    }

    private async Task<(Guid TeamId, string TeamName, Guid LeaderStudentId, Guid LeaderUserId)?> ResolveAcceptedProjectTeamAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        var acceptedTeamId = await dbContext.Applications.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.Status == ApplicationStatus.ACCEPTED && a.TeamId.HasValue)
            .Select(a => a.TeamId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!acceptedTeamId.HasValue)
        {
            acceptedTeamId = await dbContext.Teams.AsNoTracking()
                .Where(t => t.TargetProjectId == projectId && t.IsLocked)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!acceptedTeamId.HasValue) return null;

        var info = await (
            from t in dbContext.Teams.AsNoTracking()
            where t.Id == acceptedTeamId.Value
            let leaderSid = dbContext.TeamMembers.AsNoTracking()
                .Where(m => m.TeamId == t.Id && m.Role == TeamMemberRole.LEADER)
                .Select(m => (Guid?)m.StudentId)
                .FirstOrDefault() ?? t.CreatedByStudentId
            join sp in dbContext.StudentProfiles.AsNoTracking() on leaderSid equals sp.Id
            select new { t.Id, t.Name, LeaderStudentId = leaderSid, LeaderUserId = sp.UserId })
            .SingleOrDefaultAsync(cancellationToken);

        return info is null ? null : (info.Id, info.Name, info.LeaderStudentId, info.LeaderUserId);
    }

    private static List<Guid> GetApplicationNotifyStudentIds(Domain.Applications.Application application)
    {
        if (application.Members.Count > 0)
        {
            return application.Members.Select(m => m.StudentId).Distinct().ToList();
        }

        return [application.StudentId];
    }

    private async Task NotifyCompanyAsync(Guid projectId, Guid companyId, string type, string title, object payload, CancellationToken cancellationToken)
    {
        if (notificationWriter is null) return;
        var companyUserIds = await dbContext.CompanyMembers.AsNoTracking()
            .Where(cm => cm.CompanyId == companyId)
            .Select(cm => cm.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var json = JsonSerializer.Serialize(payload);
        foreach (var uid in companyUserIds)
        {
            notificationWriter.Add(new NotificationMessage(uid, type, title, json));
        }
    }

    private async Task<Guid?> ResolveStudentIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.StudentProfiles.AsTracking()
            .Include(item => item.Privacy)
            .SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new Domain.Students.StudentProfile(userId);
            dbContext.StudentProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return profile.Id;
    }

    private async Task<ApplicationResponse?> ProjectApplicationAsync(Guid applicationId, Guid studentId, CancellationToken cancellationToken)
    {
        var authorized = await dbContext.Applications.AsNoTracking()
            .AnyAsync(a => a.Id == applicationId && (a.StudentId == studentId || a.Members.Any(m => m.StudentId == studentId)), cancellationToken);
        if (!authorized) return null;

        var items = await ProjectApplicationsByIdsAsync([applicationId], cancellationToken);
        return items.SingleOrDefault();
    }

    private async Task<IReadOnlyCollection<ApplicationResponse>> ProjectApplicationsByIdsAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken)
    {
        if (applicationIds.Count == 0) return [];

        var apps = await dbContext.Applications.AsNoTracking()
            .Where(a => applicationIds.Contains(a.Id))
            .Include(a => a.Project).ThenInclude(p => p.Company)
            .Include(a => a.Team)
            .Include(a => a.Student)
            .ToListAsync(cancellationToken);

        var orderMap = applicationIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
        apps = apps.OrderBy(a => orderMap.GetValueOrDefault(a.Id, int.MaxValue)).ToList();

        var membersByApp = await LoadApplicationMembersAsync(applicationIds, cancellationToken);

        var leaderUserIds = apps.Select(a => a.Student.UserId).Distinct().ToList();
        var leaderNames = leaderUserIds.Count == 0 ? new Dictionary<Guid, string>() : await dbContext.Users.AsNoTracking()
            .Where(u => leaderUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var lecturerIds = apps.Where(a => a.RecommendedByLecturerId.HasValue).Select(a => a.RecommendedByLecturerId!.Value).Distinct().ToList();
        var lecturerNames = lecturerIds.Count == 0 ? new Dictionary<Guid, string>() : await (
            from lp in dbContext.LecturerProfiles.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on lp.UserId equals u.Id
            where lecturerIds.Contains(lp.Id)
            select new { lp.Id, u.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var joinTeamIds = apps.Where(a => a.JoinTargetTeamId.HasValue).Select(a => a.JoinTargetTeamId!.Value).Distinct().ToList();
        var joinTeams = joinTeamIds.Count == 0
            ? new Dictionary<Guid, (string Name, string? AvatarUrl)>()
            : (await dbContext.Teams.AsNoTracking()
                .Where(t => joinTeamIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.AvatarUrl })
                .ToListAsync(cancellationToken))
                .ToDictionary(t => t.Id, t => (t.Name, t.AvatarUrl));

        var conflictMap = await DetectConflictsForApplicationsAsync(apps, membersByApp, cancellationToken);

        return apps.Select(a =>
        {
            var members = membersByApp.GetValueOrDefault(a.Id, []);
            var (hasConflict, conflictMsg) = conflictMap.GetValueOrDefault(a.Id, (false, null));
            (string Name, string? AvatarUrl) joinTeamInfo = default;
            var hasJoinTeam = a.JoinTargetTeamId.HasValue && joinTeams.TryGetValue(a.JoinTargetTeamId.Value, out joinTeamInfo);
            return new ApplicationResponse(
                a.Id,
                a.ProjectId,
                a.Project.Title,
                a.Project.Slug,
                a.Project.Company.Name,
                a.Status.ToString(),
                a.CoverLetter,
                a.CreatedAt,
                a.WithdrawnAt,
                a.ApplicationType.ToString(),
                a.TeamId,
                a.Team?.Name,
                a.StudentId,
                leaderNames.GetValueOrDefault(a.Student.UserId, DisplayNameFallbacks.Student),
                a.GithubUrl,
                a.PortfolioUrl,
                a.CvUrl,
                a.CvFileName,
                a.Availability,
                a.RecommendedByLecturerId,
                a.RecommendedByLecturerId.HasValue ? lecturerNames.GetValueOrDefault(a.RecommendedByLecturerId.Value) : null,
                a.InterviewScheduleNote,
                a.DecisionReason,
                a.AdjustmentProposalJson,
                a.CompanyOfferConditions,
                hasConflict,
                conflictMsg,
                a.IsPreferredInConflict,
                a.PriorityOrder,
                members,
                a.JoinTargetTeamId,
                hasJoinTeam ? joinTeamInfo.Name : null,
                a.JoinProposedRole,
                a.Team?.AvatarUrl,
                hasJoinTeam ? joinTeamInfo.AvatarUrl : null,
                a.Project.CompanyId);
        }).ToList();
    }

    private async Task<IReadOnlyCollection<CompanyApplicationResponse>> ProjectCompanyApplicationsByIdsAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken)
    {
        if (applicationIds.Count == 0) return [];

        var apps = await dbContext.Applications.AsNoTracking()
            .Where(a => applicationIds.Contains(a.Id))
            .Include(a => a.Project)
            .Include(a => a.Team)
            .Include(a => a.Student)
            .ToListAsync(cancellationToken);

        var orderMap = applicationIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
        apps = apps.OrderBy(a => orderMap.GetValueOrDefault(a.Id, int.MaxValue)).ToList();

        var studentIds = apps.Select(a => a.StudentId).Distinct().ToList();
        var userIds = apps.Select(a => a.Student.UserId).Distinct().ToList();

        var users = await dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var majorIds = apps.Where(a => a.Student.MajorId.HasValue).Select(a => a.Student.MajorId!.Value).Distinct().ToList();
        var majors = majorIds.Count == 0 ? new Dictionary<Guid, string>() : await dbContext.Majors.AsNoTracking()
            .Where(m => majorIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var studentSkills = await dbContext.StudentSkills.AsNoTracking()
            .Where(s => studentIds.Contains(s.StudentId))
            .Join(dbContext.Skills.AsNoTracking(), s => s.SkillId, sk => sk.Id, (s, sk) => new { s.StudentId, sk.Name })
            .ToListAsync(cancellationToken);
        var skillsByStudent = studentSkills.GroupBy(x => x.StudentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<string>)g.Select(x => x.Name).Distinct().ToList());

        var membersByApp = await LoadApplicationMembersAsync(applicationIds, cancellationToken);

        var lecturerIds = apps.Where(a => a.RecommendedByLecturerId.HasValue).Select(a => a.RecommendedByLecturerId!.Value).Distinct().ToList();
        var lecturers = lecturerIds.Count == 0
            ? new Dictionary<Guid, (string Name, string? Department)>()
            : (await (
                from lp in dbContext.LecturerProfiles.AsNoTracking()
                join u in dbContext.Users.AsNoTracking() on lp.UserId equals u.Id
                where lecturerIds.Contains(lp.Id)
                select new { lp.Id, Name = u.DisplayName, lp.Department })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.Id, x => (Name: x.Name, Department: (string?)x.Department));

        var appTeamIds = apps.Where(a => a.TeamId.HasValue).Select(a => a.TeamId!.Value).Distinct().ToList();
        var appProjectIds = apps.Select(a => a.ProjectId).Distinct().ToList();
        var teamRecs = appTeamIds.Count == 0
            ? []
            : await dbContext.TeamProjectRecommendations.AsNoTracking()
                .Where(r => appTeamIds.Contains(r.TeamId) && appProjectIds.Contains(r.ProjectId))
                .ToListAsync(cancellationToken);

        var joinTeamIds = apps.Where(a => a.JoinTargetTeamId.HasValue).Select(a => a.JoinTargetTeamId!.Value).Distinct().ToList();
        var joinTeams = joinTeamIds.Count == 0
            ? new Dictionary<Guid, (string Name, string? AvatarUrl)>()
            : (await dbContext.Teams.AsNoTracking()
                .Where(t => joinTeamIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.AvatarUrl })
                .ToListAsync(cancellationToken))
                .ToDictionary(t => t.Id, t => (t.Name, t.AvatarUrl));

        var conflictMap = await DetectConflictsForApplicationsAsync(apps, membersByApp, cancellationToken);

        return apps.Select(a =>
        {
            users.TryGetValue(a.Student.UserId, out var user);
            var members = membersByApp.GetValueOrDefault(a.Id, []);
            var (hasConflict, conflictWarning) = conflictMap.GetValueOrDefault(a.Id, (false, null));
            string? lecturerName = null;
            string? lecturerDept = null;
            string? recReason = null;

            if (a.RecommendedByLecturerId.HasValue && lecturers.TryGetValue(a.RecommendedByLecturerId.Value, out var lec))
            {
                lecturerName = lec.Name;
                lecturerDept = lec.Department;
                recReason = teamRecs.FirstOrDefault(r => r.TeamId == a.TeamId && r.ProjectId == a.ProjectId && r.LecturerId == a.RecommendedByLecturerId.Value)?.Reason;
            }

            (string Name, string? AvatarUrl) joinTeamInfo = default;
            var hasJoinTeam = a.JoinTargetTeamId.HasValue && joinTeams.TryGetValue(a.JoinTargetTeamId.Value, out joinTeamInfo);

            return new CompanyApplicationResponse(
                a.Id,
                a.ProjectId,
                a.Project.Title,
                a.Project.Slug,
                a.Status.ToString(),
                user?.DisplayName ?? DisplayNameFallbacks.Student,
                a.Student.StudentCode,
                a.CvUrl ?? a.Student.CvUrl,
                a.PortfolioUrl ?? a.Student.PortfolioUrl,
                a.GithubUrl ?? a.Student.GithubUrl,
                a.CoverLetter,
                a.CreatedAt,
                a.DecidedAt,
                a.DecisionReason,
                a.ApplicationType.ToString(),
                a.TeamId,
                a.Team?.Name,
                a.Team?.Description,
                user?.Email,
                a.Student.MajorId.HasValue ? majors.GetValueOrDefault(a.Student.MajorId.Value) : null,
                a.Student.AvatarUrl,
                a.CvFileName,
                a.Availability,
                skillsByStudent.GetValueOrDefault(a.StudentId, []),
                members,
                a.RecommendedByLecturerId,
                lecturerName,
                lecturerDept,
                recReason,
                hasConflict,
                conflictWarning,
                a.InterviewScheduleNote,
                a.AdjustmentProposalJson,
                a.CompanyOfferConditions,
                a.StudentId,
                a.JoinTargetTeamId,
                hasJoinTeam ? joinTeamInfo.Name : null,
                a.JoinProposedRole,
                a.Team?.AvatarUrl,
                hasJoinTeam ? joinTeamInfo.AvatarUrl : null,
                a.Student.UserId);
        }).ToList();
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<ApplicationMemberResponse>>> LoadApplicationMembersAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken)
    {
        var raw = await (
            from am in dbContext.ApplicationMembers.AsNoTracking()
            join sp in dbContext.StudentProfiles.AsNoTracking() on am.StudentId equals sp.Id
            join u in dbContext.Users.AsNoTracking() on sp.UserId equals u.Id
            where applicationIds.Contains(am.ApplicationId)
            orderby am.CreatedAt
            select new
            {
                am.Id,
                am.ApplicationId,
                am.StudentId,
                sp.UserId,
                u.DisplayName,
                sp.StudentCode,
                u.Email,
                sp.AvatarUrl,
                am.ProposedRole,
                ConsentStatus = am.ConsentStatus.ToString(),
                am.IsSelectedByCompany,
                am.RespondedAt,
                am.Note
            }).ToListAsync(cancellationToken);

        if (raw.Count == 0) return [];

        var memberStudentIds = raw.Select(r => r.StudentId).Distinct().ToList();
        var skillsRows = await dbContext.StudentSkills.AsNoTracking()
            .Where(s => memberStudentIds.Contains(s.StudentId))
            .Join(dbContext.Skills.AsNoTracking(), s => s.SkillId, sk => sk.Id, (s, sk) => new { s.StudentId, sk.Name })
            .ToListAsync(cancellationToken);
        var skillsByStudent = skillsRows.GroupBy(x => x.StudentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<string>)g.Select(x => x.Name).Distinct().Take(6).ToList());

        return raw.GroupBy(r => r.ApplicationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<ApplicationMemberResponse>)g.Select(r => new ApplicationMemberResponse(
                    r.Id,
                    r.StudentId,
                    r.UserId,
                    r.DisplayName,
                    r.StudentCode,
                    r.Email,
                    r.AvatarUrl,
                    r.ProposedRole,
                    r.ConsentStatus,
                    r.IsSelectedByCompany,
                    r.RespondedAt,
                    r.Note,
                    skillsByStudent.GetValueOrDefault(r.StudentId, []))).ToList());
    }

    private async Task<Dictionary<Guid, (bool HasConflict, string? Message)>> DetectConflictsForApplicationsAsync(
        IReadOnlyCollection<Domain.Applications.Application> apps,
        Dictionary<Guid, IReadOnlyCollection<ApplicationMemberResponse>> membersByApp,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, (bool, string?)>();
        var projectIds = apps.Select(a => a.ProjectId).Distinct().ToList();
        if (projectIds.Count == 0) return result;

        var activeAppsInProjects = await dbContext.Applications.AsNoTracking()
            .Where(a => projectIds.Contains(a.ProjectId) && ActiveConflictStatuses.Contains(a.Status))
            .Select(a => new
            {
                a.Id,
                a.ProjectId,
                a.StudentId,
                a.ApplicationType,
                MemberIds = a.Members.Select(m => m.StudentId).ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var app in apps)
        {
            if (!ActiveConflictStatuses.Contains(app.Status))
            {
                result[app.Id] = (false, null);
                continue;
            }

            var myMembers = membersByApp.GetValueOrDefault(app.Id, []);
            var myStudentIds = myMembers.Count > 0
                ? myMembers.Select(m => m.StudentId).ToHashSet()
                : [app.StudentId];

            var overlapping = activeAppsInProjects
                .Where(other => other.Id != app.Id && other.ProjectId == app.ProjectId)
                .Where(other =>
                {
                    var otherStudents = other.MemberIds.Count > 0 ? other.MemberIds : [other.StudentId];
                    return otherStudents.Any(sid => myStudentIds.Contains(sid));
                })
                .ToList();

            if (overlapping.Count > 0)
            {
                result[app.Id] = (true, "Có sinh viên vừa nộp đơn cá nhân vừa tham gia đơn nhóm cho cùng bài toán này. Vui lòng xác nhận lựa chọn ưu tiên trước khi chốt.");
            }
            else
            {
                result[app.Id] = (false, null);
            }
        }

        return result;
    }
}
