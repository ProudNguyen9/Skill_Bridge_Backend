using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Commitments;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the commitment data operations.</summary>
public sealed class CommitmentRepository(AppDbContext dbContext) : ICommitmentRepository
{
    public Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken) => dbContext.StudentProfiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<CommitmentResponse>> ListCommitmentsAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var rows = await (
            from item in dbContext.ProjectCommitments.AsNoTracking()
            join p in dbContext.Projects.AsNoTracking() on item.ProjectId equals p.Id
            where item.StudentId == studentId
            orderby item.CreatedAt descending
            select new
            {
                item.Id,
                item.ProjectId,
                item.StudentId,
                item.Status,
                item.PolicyVersion,
                item.ConfirmationDeadline,
                item.ConfirmedAt,
                item.ApplicationId,
                item.ProposedRole,
                item.Conditions,
                item.AllowanceShareAmount,
                item.DeclinedAt,
                item.DeclineReason,
                item.StudentSignerName,
                item.StudentSignatureData,
                p.CompanyCommitmentSignerName,
                p.CompanyCommitmentSignerTitle,
                p.CompanyCommitmentSignatureData,
                p.CompanyCommitmentSignedAt,
                p.UniversityCommitmentSignerName,
                p.UniversityCommitmentSignerTitle,
                p.UniversityCommitmentSignatureData,
                p.UniversityCommitmentSignedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new CommitmentResponse(
            item.Id,
            item.ProjectId,
            item.StudentId,
            item.Status,
            item.PolicyVersion,
            item.ConfirmationDeadline,
            item.ConfirmedAt,
            item.ApplicationId,
            item.ProposedRole,
            item.Conditions,
            item.AllowanceShareAmount,
            item.DeclinedAt,
            item.DeclineReason,
            item.StudentSignerName,
            item.StudentSignatureData,
            new PartySignatureInfoResponse(
                item.CompanyCommitmentSignedAt.HasValue,
                item.CompanyCommitmentSignerName,
                item.CompanyCommitmentSignerTitle,
                item.CompanyCommitmentSignatureData,
                item.CompanyCommitmentSignedAt),
            new PartySignatureInfoResponse(
                item.UniversityCommitmentSignedAt.HasValue,
                item.UniversityCommitmentSignerName,
                item.UniversityCommitmentSignerTitle,
                item.UniversityCommitmentSignatureData,
                item.UniversityCommitmentSignedAt)))
            .ToList();
    }

    public async Task<CommitmentResponse?> FindCommitmentAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken)
    {
        var item = await (
            from pc in dbContext.ProjectCommitments.AsNoTracking()
            join p in dbContext.Projects.AsNoTracking() on pc.ProjectId equals p.Id
            where pc.ProjectId == projectId && pc.StudentId == studentId
            select new
            {
                pc.Id,
                pc.ProjectId,
                pc.StudentId,
                pc.Status,
                pc.PolicyVersion,
                pc.ConfirmationDeadline,
                pc.ConfirmedAt,
                pc.ApplicationId,
                pc.ProposedRole,
                pc.Conditions,
                pc.AllowanceShareAmount,
                pc.DeclinedAt,
                pc.DeclineReason,
                pc.StudentSignerName,
                pc.StudentSignatureData,
                p.CompanyCommitmentSignerName,
                p.CompanyCommitmentSignerTitle,
                p.CompanyCommitmentSignatureData,
                p.CompanyCommitmentSignedAt,
                p.UniversityCommitmentSignerName,
                p.UniversityCommitmentSignerTitle,
                p.UniversityCommitmentSignatureData,
                p.UniversityCommitmentSignedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null) return null;

        return new CommitmentResponse(
            item.Id,
            item.ProjectId,
            item.StudentId,
            item.Status,
            item.PolicyVersion,
            item.ConfirmationDeadline,
            item.ConfirmedAt,
            item.ApplicationId,
            item.ProposedRole,
            item.Conditions,
            item.AllowanceShareAmount,
            item.DeclinedAt,
            item.DeclineReason,
            item.StudentSignerName,
            item.StudentSignatureData,
            new PartySignatureInfoResponse(
                item.CompanyCommitmentSignedAt.HasValue,
                item.CompanyCommitmentSignerName,
                item.CompanyCommitmentSignerTitle,
                item.CompanyCommitmentSignatureData,
                item.CompanyCommitmentSignedAt),
            new PartySignatureInfoResponse(
                item.UniversityCommitmentSignedAt.HasValue,
                item.UniversityCommitmentSignerName,
                item.UniversityCommitmentSignerTitle,
                item.UniversityCommitmentSignatureData,
                item.UniversityCommitmentSignedAt));
    }

    public Task<ProjectCommitment?> FindCommitmentForUpdateAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) => dbContext.ProjectCommitments.AsTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);

    public Task<Project?> FindProjectForUpdateAsync(Guid projectId, CancellationToken cancellationToken) => dbContext.Projects.AsTracking().SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);

    public Task<bool> HasProjectMembershipAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);

    public void AddProjectMember(ProjectMember member) => dbContext.ProjectMembers.Add(member);

    public void AddProjectActivity(ProjectActivity activity) => dbContext.ProjectActivities.Add(activity);

    public Task<ProjectMember?> FindActiveMembershipAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking().SingleOrDefaultAsync(item => item.StudentId == studentId && item.IsActive && item.ProjectId == projectId, cancellationToken);

    public Task<bool> HasOpenWithdrawalAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.WithdrawalRequests.AnyAsync(item => item.ProjectId == projectId && item.StudentId == studentId && (item.Status == WithdrawalStatus.REQUESTED || item.Status == WithdrawalStatus.RECOMMENDED), cancellationToken);

    public void AddWithdrawalRequest(WithdrawalRequest withdrawal) => dbContext.WithdrawalRequests.Add(withdrawal);

    public async Task<IReadOnlyCollection<WithdrawalResponse>> ListWithdrawalsAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.WithdrawalRequests.AsNoTracking().Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.CreatedAt).Select(item => new WithdrawalResponse(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt)).ToListAsync(cancellationToken);

    public Task<WithdrawalResponse?> FindWithdrawalAsync(Guid studentId, Guid withdrawalId, CancellationToken cancellationToken) =>
        dbContext.WithdrawalRequests.AsNoTracking().Where(item => item.Id == withdrawalId && item.StudentId == studentId)
            .Select(item => new WithdrawalResponse(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<AdminWithdrawalListItemResponse>> ListWithdrawalsAdminAsync(
        AdminWithdrawalListQuery query, CancellationToken cancellationToken)
    {
        var withdrawals = from request in dbContext.WithdrawalRequests.AsNoTracking()
                          join project in dbContext.Projects.AsNoTracking() on request.ProjectId equals project.Id
                          join student in dbContext.StudentProfiles.AsNoTracking() on request.StudentId equals student.Id
                          join studentUser in dbContext.Users.AsNoTracking() on student.UserId equals studentUser.Id
                          select new { request, project, studentUser };

        if (query.Status is not null && Enum.TryParse<WithdrawalStatus>(query.Status, ignoreCase: true, out var status))
            withdrawals = withdrawals.Where(item => item.request.Status == status);

        var totalItems = await withdrawals.CountAsync(cancellationToken);
        var metadata = PageMetadata.Create(query.Page, query.PageSize, totalItems);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalItems)
            return new PagedResponse<AdminWithdrawalListItemResponse>([], metadata);

        var items = await withdrawals
            .OrderByDescending(item => item.request.CreatedAt)
            .ThenBy(item => item.request.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(item => new AdminWithdrawalListItemResponse(
                item.request.Id, item.request.ProjectId, item.project.Title,
                item.request.StudentId, item.studentUser.DisplayName, item.studentUser.Email,
                item.request.Reason, item.request.Status, item.request.LecturerNote,
                item.request.RecommendedAt, item.request.DecidedAt, item.request.DecisionNote,
                item.request.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminWithdrawalListItemResponse>(items, metadata);
    }

    public Task<Guid?> FindLecturerIdAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsNoTracking().Where(item => item.UserId == lecturerUserId && item.IsActive).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<WithdrawalRequest?> FindWithdrawalForUpdateAsync(Guid withdrawalId, CancellationToken cancellationToken) =>
        dbContext.WithdrawalRequests.AsTracking().SingleOrDefaultAsync(item => item.Id == withdrawalId, cancellationToken);

    public Task<bool> IsLecturerAssignedAsync(Guid projectId, Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.AnyAsync(item => item.ProjectId == projectId && item.LecturerId == lecturerId && item.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE, cancellationToken);

    public async Task DeactivateActiveMembershipsAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers
            .Where(item => item.ProjectId == projectId && item.StudentId == studentId && item.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false), cancellationToken);

    public Task<List<ProjectCommitment>> FindPendingCommitmentsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.ProjectCommitments.AsTracking()
            .Where(item => item.Status == CommitmentStatus.PENDING && item.ConfirmationDeadline <= now)
            .ToListAsync(cancellationToken);

    public async Task<bool> IsTeamLeaderForCommitmentAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken)
    {
        var isTeamLeader = await dbContext.Applications.AsNoTracking()
            .AnyAsync(a => a.ProjectId == projectId &&
                           a.Status == DNTU.SkillBridge.Domain.Applications.ApplicationStatus.ACCEPTED &&
                           a.TeamId.HasValue &&
                           dbContext.TeamMembers.Any(tm => tm.TeamId == a.TeamId && tm.StudentId == studentId && tm.Role == DNTU.SkillBridge.Domain.Teams.TeamMemberRole.LEADER),
                cancellationToken);
        if (isTeamLeader) return true;

        var proposedRole = await dbContext.ProjectCommitments.AsNoTracking()
            .Where(c => c.ProjectId == projectId && c.StudentId == studentId)
            .Select(c => c.ProposedRole)
            .SingleOrDefaultAsync(cancellationToken);

        return proposedRole is not null &&
               (proposedRole.Contains("Trưởng nhóm", StringComparison.OrdinalIgnoreCase) ||
                proposedRole.Contains("Leader", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProjectCommitmentOverviewResponse?> GetProjectOverviewAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var proj = await (
            from p in dbContext.Projects.AsNoTracking()
            join c in dbContext.Companies.AsNoTracking() on p.CompanyId equals c.Id
            where p.Id == projectId
            select new
            {
                p.Id,
                p.Code,
                p.Title,
                Status = p.Status.ToString(),
                CompanyName = c.Name,
                p.AllowanceAmount,
                p.AllowanceCurrency,
                p.CompanyCommitmentSignerName,
                p.CompanyCommitmentSignerTitle,
                p.CompanyCommitmentSignatureData,
                p.CompanyCommitmentSignedAt,
                p.UniversityCommitmentSignerName,
                p.UniversityCommitmentSignerTitle,
                p.UniversityCommitmentSignatureData,
                p.UniversityCommitmentSignedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (proj is null) return null;

        var memberRows = await (
            from pc in dbContext.ProjectCommitments.AsNoTracking()
            join sp in dbContext.StudentProfiles.AsNoTracking() on pc.StudentId equals sp.Id
            join u in dbContext.Users.AsNoTracking() on sp.UserId equals u.Id
            where pc.ProjectId == projectId
            orderby pc.CreatedAt
            select new
            {
                CommitmentId = pc.Id,
                pc.StudentId,
                u.DisplayName,
                sp.StudentCode,
                u.Email,
                pc.Status,
                pc.ProposedRole,
                pc.AllowanceShareAmount,
                pc.ConfirmationDeadline,
                pc.ConfirmedAt,
                pc.DeclinedAt,
                pc.DeclineReason,
                pc.StudentSignerName,
                pc.StudentSignatureData,
                pc.Conditions,
                pc.PolicyVersion
            })
            .ToListAsync(cancellationToken);

        var members = memberRows
            .Select(pc => new ProjectCommitmentMemberItemResponse(
                pc.CommitmentId,
                pc.StudentId,
                pc.DisplayName,
                pc.StudentCode,
                pc.Email,
                pc.Status,
                pc.ProposedRole,
                pc.AllowanceShareAmount,
                pc.ConfirmationDeadline,
                pc.ConfirmedAt,
                pc.DeclinedAt,
                pc.DeclineReason,
                pc.StudentSignerName ?? pc.DisplayName,
                pc.StudentSignatureData))
            .ToList();

        var lecturer = await (
            from la in dbContext.LecturerAssignments.AsNoTracking()
            join lp in dbContext.LecturerProfiles.AsNoTracking() on la.LecturerId equals lp.Id
            join u in dbContext.Users.AsNoTracking() on lp.UserId equals u.Id
            where la.ProjectId == projectId &&
                  (la.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE ||
                   la.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.INVITED)
            orderby la.AssignedAt descending
            select new { u.DisplayName, Status = la.Status.ToString(), la.AllowanceShareAmount })
            .FirstOrDefaultAsync(cancellationToken);

        var firstCondition = memberRows.Select(m => m.Conditions).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
        var policyVersion = memberRows.Select(m => m.PolicyVersion).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "v1";

        return new ProjectCommitmentOverviewResponse(
            proj.Id,
            proj.Title,
            proj.CompanyName,
            proj.AllowanceAmount,
            proj.AllowanceCurrency,
            lecturer?.DisplayName,
            lecturer?.Status,
            lecturer?.AllowanceShareAmount,
            members,
            proj.Code,
            proj.Status,
            firstCondition,
            policyVersion,
            new PartySignatureInfoResponse(
                proj.CompanyCommitmentSignedAt.HasValue,
                proj.CompanyCommitmentSignerName,
                proj.CompanyCommitmentSignerTitle,
                proj.CompanyCommitmentSignatureData,
                proj.CompanyCommitmentSignedAt),
            new PartySignatureInfoResponse(
                proj.UniversityCommitmentSignedAt.HasValue,
                proj.UniversityCommitmentSignerName,
                proj.UniversityCommitmentSignerTitle,
                proj.UniversityCommitmentSignatureData,
                proj.UniversityCommitmentSignedAt));
    }

    public async Task<IReadOnlyCollection<Guid>> ListProjectCompanyUserIdsAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .SelectMany(p => p.Company.Members.Select(m => m.UserId))
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasCompanyAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.Company.Members.Any(m => m.UserId == userId), cancellationToken);

    public Task<bool> HasLecturerProjectAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        (from la in dbContext.LecturerAssignments.AsNoTracking()
         join lp in dbContext.LecturerProfiles.AsNoTracking() on la.LecturerId equals lp.Id
         where la.ProjectId == projectId &&
               lp.UserId == userId &&
               la.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE
         select la.Id)
        .AnyAsync(cancellationToken);

    public Task<IReadOnlyCollection<Guid>> ListProjectCommittedStudentUserIdsAsync(Guid projectId, CancellationToken cancellationToken) =>
        (from pc in dbContext.ProjectCommitments.AsNoTracking()
         join sp in dbContext.StudentProfiles.AsNoTracking() on pc.StudentId equals sp.Id
         where pc.ProjectId == projectId && pc.Status != CommitmentStatus.DECLINED && pc.Status != CommitmentStatus.ABANDONED
         select sp.UserId)
        .Distinct()
        .ToListAsync(cancellationToken)
        .ContinueWith<IReadOnlyCollection<Guid>>(t => t.Result, cancellationToken);

    public Task<Guid?> FindActiveLecturerUserIdAsync(Guid projectId, CancellationToken cancellationToken) =>
        (from la in dbContext.LecturerAssignments.AsNoTracking()
         join lp in dbContext.LecturerProfiles.AsNoTracking() on la.LecturerId equals lp.Id
         where la.ProjectId == projectId && la.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE
         select (Guid?)lp.UserId)
        .FirstOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>> ListProjectCommitmentSummariesForAdminAsync(CancellationToken cancellationToken) =>
        ListProjectCommitmentSummariesFilteredAsync(null, null, cancellationToken);

    public Task<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>> ListProjectCommitmentSummariesForCompanyAsync(Guid userId, CancellationToken cancellationToken) =>
        ListProjectCommitmentSummariesFilteredAsync(userId, null, cancellationToken);

    public Task<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>> ListProjectCommitmentSummariesForLecturerAsync(Guid userId, CancellationToken cancellationToken) =>
        ListProjectCommitmentSummariesFilteredAsync(null, userId, cancellationToken);

    private async Task<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>> ListProjectCommitmentSummariesFilteredAsync(
        Guid? companyUserId, Guid? lecturerUserId, CancellationToken cancellationToken)
    {
        var query = from p in dbContext.Projects.AsNoTracking()
                    join c in dbContext.Companies.AsNoTracking() on p.CompanyId equals c.Id
                    where dbContext.ProjectCommitments.Any(pc => pc.ProjectId == p.Id) ||
                          p.CompanyCommitmentSignedAt.HasValue ||
                          p.UniversityCommitmentSignedAt.HasValue ||
                          p.Status == ProjectStatus.IN_PROGRESS ||
                          p.Status == ProjectStatus.COMPLETED
                    select new
                    {
                        p.Id,
                        p.Code,
                        p.Title,
                        Status = p.Status,
                        p.CompanyId,
                        CompanyName = c.Name,
                        p.CompanyCommitmentSignerName,
                        p.CompanyCommitmentSignedAt,
                        p.UniversityCommitmentSignerName,
                        p.UniversityCommitmentSignedAt,
                        p.UpdatedAt,
                        HasCompanyMember = companyUserId == null || c.Members.Any(m => m.UserId == companyUserId),
                        HasLecturer = lecturerUserId == null || dbContext.LecturerAssignments.Any(la =>
                            la.ProjectId == p.Id &&
                            la.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE &&
                            dbContext.LecturerProfiles.Any(lp => lp.Id == la.LecturerId && lp.UserId == lecturerUserId))
                    };

        var projects = await query
            .Where(x => x.HasCompanyMember && x.HasLecturer)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        if (projects.Count == 0) return [];

        var projectIds = projects.Select(x => x.Id).ToList();
        var commitmentCounts = await dbContext.ProjectCommitments.AsNoTracking()
            .Where(pc => projectIds.Contains(pc.ProjectId) && pc.Status != CommitmentStatus.DECLINED && pc.Status != CommitmentStatus.ABANDONED)
            .GroupBy(pc => pc.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Total = g.Count(),
                Confirmed = g.Count(x => x.Status == CommitmentStatus.CONFIRMED),
                Pending = g.Count(x => x.Status == CommitmentStatus.PENDING)
            })
            .ToDictionaryAsync(x => x.ProjectId, cancellationToken);

        return projects.Select(p =>
        {
            var counts = commitmentCounts.GetValueOrDefault(p.Id);
            return new ProjectCommitmentSummaryItemResponse(
                p.Id,
                p.Code,
                p.Title,
                p.Status.ToString(),
                p.CompanyId,
                p.CompanyName,
                counts?.Total ?? 0,
                counts?.Confirmed ?? 0,
                counts?.Pending ?? 0,
                p.CompanyCommitmentSignedAt.HasValue,
                p.CompanyCommitmentSignerName,
                p.CompanyCommitmentSignedAt,
                p.UniversityCommitmentSignedAt.HasValue,
                p.UniversityCommitmentSignerName,
                p.UniversityCommitmentSignedAt);
        }).ToList();
    }
}
