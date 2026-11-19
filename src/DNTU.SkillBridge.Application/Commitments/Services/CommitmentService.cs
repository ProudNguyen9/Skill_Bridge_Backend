using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Commitments;

public sealed class CommitmentService(
    ICommitmentRepository commitmentRepository,
    IUnitOfWork unitOfWork,
    IProjectRealtimeNotifier? realtimeNotifier = null,
    INotificationWriter? notificationWriter = null) : ICommitmentService
{
    private static readonly string[] AllowedSignaturePrefixes =
    [
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/jpg;base64,",
        "data:image/webp;base64,"
    ];

    public async Task<CommitmentResponse?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await commitmentRepository.FindCommitmentAsync(projectId, studentId.Value, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommitmentResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];
        return await commitmentRepository.ListCommitmentsAsync(studentId.Value, cancellationToken);
    }

    public Task<CommitmentResponse?> ConfirmAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        ConfirmAsync(userId, projectId, null, cancellationToken);

    public async Task<CommitmentResponse?> ConfirmAsync(
        Guid userId, Guid projectId, SignCommitmentRequest? request, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return null;

        var sanitizedSignature = SanitizeSignatureDataUrl(request?.SignatureDataUrl);
        if (request?.SignatureDataUrl is not null && !string.IsNullOrWhiteSpace(request.SignatureDataUrl) && sanitizedSignature is null)
        {
            return null;
        }

        var signerName = !string.IsNullOrWhiteSpace(request?.SignerName)
            ? request.SignerName.Trim()
            : await commitmentRepository.FindUserDisplayNameAsync(userId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var commitment = await commitmentRepository.FindCommitmentForUpdateAsync(projectId, studentId.Value, cancellationToken);
        if (commitment is null) return null;
        try { commitment.Confirm(DateTimeOffset.UtcNow, signerName, sanitizedSignature); }
        catch (InvalidOperationException) { return null; }

        var project = await commitmentRepository.FindProjectForUpdateAsync(projectId, cancellationToken);
        var statusAdvanced = false;
        if (project is not null && project.Status != ProjectStatus.IN_PROGRESS)
        {
            project.StartExecution();
            statusAdvanced = true;
        }

        var membershipExists = await commitmentRepository.HasProjectMembershipAsync(projectId, studentId.Value, cancellationToken);
        if (!membershipExists)
        {
            var member = new ProjectMember(projectId, studentId.Value);
            var isLeader = await commitmentRepository.IsTeamLeaderForCommitmentAsync(projectId, studentId.Value, cancellationToken);
            if (isLeader)
            {
                member.AssignRole(ProjectMemberRole.LEADER);
            }
            commitmentRepository.AddProjectMember(member);
        }
        commitmentRepository.AddProjectActivity(new ProjectActivity(projectId, "COMMITMENT_CONFIRMED", userId));

        if (notificationWriter is not null)
        {
            var companyUsers = await commitmentRepository.ListProjectCompanyUserIdsAsync(projectId, cancellationToken);
            foreach (var cid in companyUsers)
            {
                notificationWriter.Add(new NotificationMessage(
                    cid,
                    "COMMITMENT_CONFIRMED",
                    $"Sinh viên đã ký xác nhận cam kết tham gia dự án \"{project?.Title ?? projectId.ToString()}\"",
                    JsonSerializer.Serialize(new { projectId, studentId = studentId.Value })));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (statusAdvanced && realtimeNotifier is not null)
        {
            await realtimeNotifier.NotifyProjectStatusChangedAsync(projectId, "IN_PROGRESS", cancellationToken);
        }

        return await commitmentRepository.FindCommitmentAsync(projectId, studentId.Value, cancellationToken);
    }

    public async Task<CommitmentResponse?> DeclineAsync(Guid userId, Guid projectId, string? reason, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return null;

        var commitment = await commitmentRepository.FindCommitmentForUpdateAsync(projectId, studentId.Value, cancellationToken);
        if (commitment is null) return null;

        try { commitment.Decline(DateTimeOffset.UtcNow, reason); }
        catch (InvalidOperationException) { return null; }

        commitmentRepository.AddProjectActivity(new ProjectActivity(projectId, "COMMITMENT_DECLINED", userId));

        if (notificationWriter is not null)
        {
            var companyUsers = await commitmentRepository.ListProjectCompanyUserIdsAsync(projectId, cancellationToken);
            foreach (var cid in companyUsers)
            {
                notificationWriter.Add(new NotificationMessage(
                    cid,
                    "COMMITMENT_DECLINED",
                    "Một sinh viên đã từ chối đề nghị tham gia dự án",
                    JsonSerializer.Serialize(new { projectId, studentId = studentId.Value, reason })));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await commitmentRepository.FindCommitmentAsync(projectId, studentId.Value, cancellationToken);
    }

    public async Task<ProjectCommitmentOverviewResponse?> GetProjectOverviewAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return null;
        var myCommitment = await commitmentRepository.FindCommitmentAsync(projectId, studentId.Value, cancellationToken);
        if (myCommitment is null && !await commitmentRepository.HasProjectMembershipAsync(projectId, studentId.Value, cancellationToken))
        {
            return null;
        }
        return await commitmentRepository.GetProjectOverviewAsync(projectId, cancellationToken);
    }

    public async Task<ProjectCommitmentOverviewResponse?> GetProjectOverviewForActorAsync(
        Guid userId, IReadOnlyCollection<string> roles, Guid projectId, CancellationToken cancellationToken)
    {
        if (roles.Contains(RoleNames.Admin) || roles.Contains(RoleNames.SuperAdmin))
        {
            return await commitmentRepository.GetProjectOverviewAsync(projectId, cancellationToken);
        }

        if (roles.Contains(RoleNames.Company) && await commitmentRepository.HasCompanyAccessAsync(userId, projectId, cancellationToken))
        {
            return await commitmentRepository.GetProjectOverviewAsync(projectId, cancellationToken);
        }

        if (roles.Contains(RoleNames.Lecturer) && await commitmentRepository.HasLecturerProjectAccessAsync(userId, projectId, cancellationToken))
        {
            return await commitmentRepository.GetProjectOverviewAsync(projectId, cancellationToken);
        }

        if (roles.Contains(RoleNames.Student))
        {
            return await GetProjectOverviewAsync(userId, projectId, cancellationToken);
        }

        return null;
    }

    public async Task<(CommitmentSignOutcome Outcome, ProjectCommitmentOverviewResponse? Overview)> SignCompanyCommitmentAsync(
        Guid userId, Guid projectId, SignCommitmentRequest? request, CancellationToken cancellationToken)
    {
        var sanitizedSignature = SanitizeSignatureDataUrl(request?.SignatureDataUrl);
        if (request?.SignatureDataUrl is not null && !string.IsNullOrWhiteSpace(request.SignatureDataUrl) && sanitizedSignature is null)
        {
            return (CommitmentSignOutcome.Conflict, null);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var project = await commitmentRepository.FindProjectForUpdateAsync(projectId, cancellationToken);
        if (project is null) return (CommitmentSignOutcome.NotFound, null);

        if (!await commitmentRepository.HasCompanyAccessAsync(userId, projectId, cancellationToken))
        {
            return (CommitmentSignOutcome.Forbidden, null);
        }

        var displayName = !string.IsNullOrWhiteSpace(request?.SignerName)
            ? request.SignerName.Trim()
            : (await commitmentRepository.FindUserDisplayNameAsync(userId, cancellationToken) ?? "Đại diện Doanh nghiệp");
        var signerTitle = string.IsNullOrWhiteSpace(request?.SignerTitle) ? "Đại diện Doanh nghiệp đối tác" : request.SignerTitle.Trim();

        try
        {
            project.SignCompanyCommitment(userId, displayName, signerTitle, sanitizedSignature, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException)
        {
            return (CommitmentSignOutcome.Conflict, null);
        }

        commitmentRepository.AddProjectActivity(new ProjectActivity(projectId, "COMPANY_COMMITMENT_SIGNED", userId));

        if (notificationWriter is not null)
        {
            var studentUsers = await commitmentRepository.ListProjectCommittedStudentUserIdsAsync(projectId, cancellationToken);
            foreach (var uid in studentUsers)
            {
                notificationWriter.Add(new NotificationMessage(
                    uid,
                    "COMPANY_COMMITMENT_SIGNED",
                    $"Doanh nghiệp đã ký xác nhận Bản cam kết B-11 cho dự án \"{project.Title}\"",
                    JsonSerializer.Serialize(new { projectId, url = $"/student/projects/{projectId}/commitment" })));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (CommitmentSignOutcome.Updated, await commitmentRepository.GetProjectOverviewAsync(projectId, cancellationToken));
    }

    public async Task<(CommitmentSignOutcome Outcome, ProjectCommitmentOverviewResponse? Overview)> SignUniversityCommitmentAsync(
        Guid userId, IReadOnlyCollection<string> roles, Guid projectId, SignCommitmentRequest? request, CancellationToken cancellationToken)
    {
        var isAdmin = roles.Contains(RoleNames.Admin) || roles.Contains(RoleNames.SuperAdmin);
        var isLecturer = roles.Contains(RoleNames.Lecturer);
        if (!isAdmin && !isLecturer)
        {
            return (CommitmentSignOutcome.Forbidden, null);
        }

        var sanitizedSignature = SanitizeSignatureDataUrl(request?.SignatureDataUrl);
        if (request?.SignatureDataUrl is not null && !string.IsNullOrWhiteSpace(request.SignatureDataUrl) && sanitizedSignature is null)
        {
            return (CommitmentSignOutcome.Conflict, null);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var project = await commitmentRepository.FindProjectForUpdateAsync(projectId, cancellationToken);
        if (project is null) return (CommitmentSignOutcome.NotFound, null);

        if (!isAdmin && !await commitmentRepository.HasLecturerProjectAccessAsync(userId, projectId, cancellationToken))
        {
            return (CommitmentSignOutcome.Forbidden, null);
        }

        var displayName = !string.IsNullOrWhiteSpace(request?.SignerName)
            ? request.SignerName.Trim()
            : (await commitmentRepository.FindUserDisplayNameAsync(userId, cancellationToken) ?? "Đại diện Nhà trường (DNTU)");
        var defaultTitle = isAdmin ? "Đại diện Trung tâm HTDN & PTKN - DNTU" : "Giảng viên hướng dẫn - DNTU";
        var signerTitle = string.IsNullOrWhiteSpace(request?.SignerTitle) ? defaultTitle : request.SignerTitle.Trim();

        try
        {
            project.SignUniversityCommitment(userId, displayName, signerTitle, sanitizedSignature, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException)
        {
            return (CommitmentSignOutcome.Conflict, null);
        }

        commitmentRepository.AddProjectActivity(new ProjectActivity(projectId, "UNIVERSITY_COMMITMENT_SIGNED", userId));

        if (notificationWriter is not null)
        {
            var recipientIds = new HashSet<Guid>(await commitmentRepository.ListProjectCommittedStudentUserIdsAsync(projectId, cancellationToken));
            foreach (var cid in await commitmentRepository.ListProjectCompanyUserIdsAsync(projectId, cancellationToken))
            {
                recipientIds.Add(cid);
            }

            foreach (var uid in recipientIds)
            {
                notificationWriter.Add(new NotificationMessage(
                    uid,
                    "UNIVERSITY_COMMITMENT_SIGNED",
                    $"Nhà trường (DNTU) đã ký chứng thực Bản cam kết B-11 cho dự án \"{project.Title}\"",
                    JsonSerializer.Serialize(new { projectId })));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (CommitmentSignOutcome.Updated, await commitmentRepository.GetProjectOverviewAsync(projectId, cancellationToken));
    }

    public Task<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>> ListProjectCommitmentSummariesAsync(
        Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        if (roles.Contains(RoleNames.Admin) || roles.Contains(RoleNames.SuperAdmin))
        {
            return commitmentRepository.ListProjectCommitmentSummariesForAdminAsync(cancellationToken);
        }

        if (roles.Contains(RoleNames.Company))
        {
            return commitmentRepository.ListProjectCommitmentSummariesForCompanyAsync(userId, cancellationToken);
        }

        if (roles.Contains(RoleNames.Lecturer))
        {
            return commitmentRepository.ListProjectCommitmentSummariesForLecturerAsync(userId, cancellationToken);
        }

        return Task.FromResult<IReadOnlyCollection<ProjectCommitmentSummaryItemResponse>>([]);
    }

    private static string? SanitizeSignatureDataUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (trimmed.Length > 500_000) return null;
        return AllowedSignaturePrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ? trimmed
            : null;
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> CreateWithdrawalAsync(Guid userId, CreateWithdrawalRequest request, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return (WithdrawalOutcome.Forbidden, null);
        var member = await commitmentRepository.FindActiveMembershipAsync(studentId.Value, request.ProjectId, cancellationToken);
        if (member is null) return (WithdrawalOutcome.NotFound, null);
        if (await commitmentRepository.HasOpenWithdrawalAsync(member.ProjectId, studentId.Value, cancellationToken)) return (WithdrawalOutcome.Conflict, null);

        var withdrawal = new WithdrawalRequest(member.ProjectId, studentId.Value, request.Reason);
        commitmentRepository.AddWithdrawalRequest(withdrawal);
        commitmentRepository.AddProjectActivity(new ProjectActivity(member.ProjectId, "WITHDRAWAL_REQUESTED", userId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (WithdrawalOutcome.Created, ToResponse(withdrawal));
    }

    public async Task<IReadOnlyCollection<WithdrawalResponse>> ListMyWithdrawalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        if (studentId is null) return [];
        return await commitmentRepository.ListWithdrawalsAsync(studentId.Value, cancellationToken);
    }

    public Task<PagedResponse<AdminWithdrawalListItemResponse>> ListWithdrawalsAdminAsync(AdminWithdrawalListQuery query, CancellationToken cancellationToken) =>
        commitmentRepository.ListWithdrawalsAdminAsync(new AdminWithdrawalListQuery
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Status = query.Status?.Trim().ToUpperInvariant()
        }, cancellationToken);

    public async Task<WithdrawalResponse?> GetMyWithdrawalAsync(Guid userId, Guid withdrawalId, CancellationToken cancellationToken)
    {
        var studentId = await commitmentRepository.FindStudentIdAsync(userId, cancellationToken);
        return studentId is null ? null : await commitmentRepository.FindWithdrawalAsync(studentId.Value, withdrawalId, cancellationToken);
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> RecommendAsync(Guid lecturerUserId, Guid withdrawalId, WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        var lecturerId = await commitmentRepository.FindLecturerIdAsync(lecturerUserId, cancellationToken);
        if (lecturerId is null) return (WithdrawalOutcome.Forbidden, null);
        var withdrawal = await commitmentRepository.FindWithdrawalForUpdateAsync(withdrawalId, cancellationToken);
        if (withdrawal is null) return (WithdrawalOutcome.NotFound, null);
        var assigned = await commitmentRepository.IsLecturerAssignedAsync(withdrawal.ProjectId, lecturerId.Value, cancellationToken);
        if (!assigned) return (WithdrawalOutcome.Forbidden, null);
        try { withdrawal.Recommend(lecturerId.Value, DateTimeOffset.UtcNow, request.Note); }
        catch (InvalidOperationException) { return (WithdrawalOutcome.Conflict, null); }
        commitmentRepository.AddProjectActivity(new ProjectActivity(withdrawal.ProjectId, "WITHDRAWAL_RECOMMENDED", lecturerUserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (WithdrawalOutcome.Updated, ToResponse(withdrawal));
    }

    public async Task<(WithdrawalOutcome Outcome, WithdrawalResponse? Withdrawal)> DecideAsync(Guid adminUserId, Guid withdrawalId, bool approve, WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var withdrawal = await commitmentRepository.FindWithdrawalForUpdateAsync(withdrawalId, cancellationToken);
        if (withdrawal is null) return (WithdrawalOutcome.NotFound, null);
        try { withdrawal.Decide(adminUserId, DateTimeOffset.UtcNow, approve, request.Note); }
        catch (InvalidOperationException) { return (WithdrawalOutcome.Conflict, null); }
        if (approve)
        {
            // Execute directly in the same transaction so withdrawal approval cannot leave
            // a stale active membership behind when the aggregate was loaded elsewhere.
            await commitmentRepository.DeactivateActiveMembershipsAsync(withdrawal.ProjectId, withdrawal.StudentId, cancellationToken);
        }
        commitmentRepository.AddProjectActivity(new ProjectActivity(withdrawal.ProjectId, approve ? "WITHDRAWAL_APPROVED" : "WITHDRAWAL_REJECTED", adminUserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (WithdrawalOutcome.Updated, ToResponse(withdrawal));
    }

    /// <summary>
    /// Job integration point for commitment-expiry processing. It is safe to invoke repeatedly;
    /// only pending commitments past their UTC deadline transition to ABANDONED.
    /// </summary>
    public async Task<int> ExpirePendingCommitmentsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await commitmentRepository.FindPendingCommitmentsAsync(now, cancellationToken);
        if (pending.Count == 0) return 0;
        foreach (var commitment in pending)
        {
            commitment.Expire(now);
            commitmentRepository.AddProjectActivity(new ProjectActivity(commitment.ProjectId, "COMMITMENT_ABANDONED", null));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }

    private static WithdrawalResponse ToResponse(WithdrawalRequest item) => new(item.Id, item.ProjectId, item.StudentId, item.Reason, item.Status, item.RecommendedByLecturerId, item.RecommendedAt, item.LecturerNote, item.DecidedByUserId, item.DecidedAt, item.DecisionNote, item.CreatedAt);
}
