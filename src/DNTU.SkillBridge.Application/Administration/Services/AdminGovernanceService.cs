using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Identity;

namespace DNTU.SkillBridge.Application.Administration;

public sealed class AdminGovernanceService(IAdministrationRepository administrationRepository, IUnitOfWork unitOfWork, INotificationWriter notificationWriter) : IAdminGovernanceService
{
    public Task<PagedResponse<AdminUserListItemResponse>> ListUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken) =>
        administrationRepository.ListUsersAsync(new AdminUserListQuery
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Search = query.Search?.Trim(),
            Role = query.Role?.Trim().ToUpperInvariant(),
            IsActive = query.IsActive
        }, cancellationToken);

    public Task<AdminUserListItemResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        administrationRepository.GetUserAsync(userId, cancellationToken);

    public Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) =>
        administrationRepository.GetDashboardAsync(cancellationToken);

    public async Task<AdminPolicyResponse?> GetPolicyAsync(string category, CancellationToken cancellationToken)
    {
        var policy = await administrationRepository.FindPolicyAsync(category, cancellationToken);
        return policy is null ? null : Map(policy);
    }

    public async Task<AdminPolicyResponse> UpdatePolicyAsync(Guid actorUserId, string category, string settingsJson, CancellationToken cancellationToken)
    {
        var policy = await administrationRepository.FindPolicyForUpdateAsync(category, cancellationToken);
        if (policy is null)
        {
            policy = new AdminPolicySetting(category, settingsJson);
            administrationRepository.AddPolicy(policy);
        }
        else
        {
            policy.Update(settingsJson);
        }

        administrationRepository.AddAuditLog(new AuditLog(actorUserId, AuditActions.PolicyUpdated, nameof(AdminPolicySetting), policy.Id.ToString(), null, null, null, null, null, "{}", DateTimeOffset.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    public Task<PagedResponse<AdminAuditLogListItemResponse>> ListAuditLogsAsync(AdminAuditLogListQuery query, CancellationToken cancellationToken) =>
        administrationRepository.ListAuditLogsAsync(new AdminAuditLogListQuery
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Search = query.Search?.Trim(),
            ActorRole = query.ActorRole?.Trim()
        }, cancellationToken);

    public async Task<AdminUserManagementOutcome> SetUserActiveAsync(Guid actorUserId, Guid targetUserId, bool isActive, CancellationToken cancellationToken)
    {
        var user = await administrationRepository.FindUserForUpdateAsync(targetUserId, cancellationToken);
        if (user is null)
        {
            return AdminUserManagementOutcome.NotFound;
        }

        var now = DateTimeOffset.UtcNow;
        if (isActive)
        {
            user.Reactivate();
            administrationRepository.AddAuditLog(BuildUserAudit(actorUserId, AuditActions.UserActivated, user, now));
        }
        else
        {
            if (user.Id == actorUserId)
            {
                return AdminUserManagementOutcome.SelfTargetForbidden;
            }

            if (user.UserRoles.Any(membership => membership.Role.NormalizedName == RoleNames.SuperAdmin))
            {
                return AdminUserManagementOutcome.PrivilegeEscalationForbidden;
            }

            user.Deactivate();
            administrationRepository.AddAuditLog(BuildUserAudit(actorUserId, AuditActions.UserDeactivated, user, now));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AdminUserManagementOutcome.Updated;
    }

    public async Task<AdminUserManagementOutcome> SetUserRolesAsync(Guid actorUserId, Guid targetUserId, IReadOnlyCollection<string> roles, bool actorIsSuperAdmin, CancellationToken cancellationToken)
    {
        var requested = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();
        if (requested.Length == 0)
        {
            return AdminUserManagementOutcome.NoRolesRequested;
        }

        var knownRoleNames = new[]
        {
            RoleNames.Student, RoleNames.Company, RoleNames.Lecturer, RoleNames.Admin, RoleNames.SuperAdmin
        };
        if (requested.Any(role => !knownRoleNames.Contains(role)))
        {
            return AdminUserManagementOutcome.UnknownRole;
        }

        // Admin accounts must never be able to escalate themselves or others into the admin tier.
        if (!actorIsSuperAdmin && requested.Any(role => role is RoleNames.Admin or RoleNames.SuperAdmin))
        {
            return AdminUserManagementOutcome.PrivilegeEscalationForbidden;
        }

        if (targetUserId == actorUserId)
        {
            return AdminUserManagementOutcome.SelfTargetForbidden;
        }

        var user = await administrationRepository.FindUserForUpdateAsync(targetUserId, cancellationToken);
        if (user is null)
        {
            return AdminUserManagementOutcome.NotFound;
        }

        var resolvedRoles = await administrationRepository.FindRolesByNamesAsync(requested, cancellationToken);
        var currentRoleIds = user.UserRoles.Select(membership => membership.RoleId).ToHashSet();
        var targetRoleIds = resolvedRoles.Select(role => role.Id).ToHashSet();

        foreach (var membership in user.UserRoles.Where(membership => !targetRoleIds.Contains(membership.RoleId)).ToList())
        {
            user.UserRoles.Remove(membership);
        }
        foreach (var role in resolvedRoles.Where(role => !currentRoleIds.Contains(role.Id)))
        {
            user.AssignRole(role.Id);
        }

        var metadataJson = System.Text.Json.JsonSerializer.Serialize(new { roles = requested });
        administrationRepository.AddAuditLog(new AuditLog(
            actorUserId, AuditActions.UserRolesUpdated, nameof(User), user.Id.ToString(),
            null, null, null, null, null, metadataJson, DateTimeOffset.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AdminUserManagementOutcome.Updated;
    }

    public async Task<AdminBroadcastResponse> BroadcastAsync(Guid actorUserId, AdminBroadcastRequest request, CancellationToken cancellationToken)
    {
        var targetRoles = request.TargetRoles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();
        var recipients = await administrationRepository.ListActiveUserIdsByRolesAsync(targetRoles, cancellationToken);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(new { broadcast = true, actorUserId });
        foreach (var recipientId in recipients)
        {
            notificationWriter.Add(new NotificationMessage(recipientId, request.Type, request.Title, payloadJson));
        }

        administrationRepository.AddAuditLog(new AuditLog(
            actorUserId, AuditActions.NotificationBroadcast, nameof(Domain.Notifications.Notification), actorUserId.ToString(),
            null, null, null, null, null,
            System.Text.Json.JsonSerializer.Serialize(new { recipients = recipients.Count, targetRoles }),
            DateTimeOffset.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AdminBroadcastResponse(recipients.Count);
    }

    private static AuditLog BuildUserAudit(Guid actorUserId, string action, User user, DateTimeOffset now) =>
        new(actorUserId, action, nameof(User), user.Id.ToString(), null, null, null, null, null, "{}", now);

    private static AdminPolicyResponse Map(AdminPolicySetting policy) => new(policy.Category, policy.SettingsJson, policy.Version, policy.UpdatedAt);
}
