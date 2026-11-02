using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Workspaces;

public sealed record WorkspaceOverviewResponse(
    Guid ProjectId,
    string Title,
    string Slug,
    string Status,
    int MemberCount,
    int OpenTaskCount,
    int OverdueTaskCount,
    IReadOnlySet<string> Capabilities,
    string? SupervisorName = null,
    string? SupervisorEmail = null,
    string? SupervisorDepartment = null,
    string? SupervisorTitle = null);

public sealed record WorkspaceMemberResponse(
    Guid StudentId,
    string DisplayName,
    DateTimeOffset JoinedAt,
    string? StudentCode = null,
    string? Email = null,
    string Role = "STUDENT",
    bool IsLeader = false);

public sealed class AssignProjectLeaderRequest
{
    [Required]
    public Guid StudentId { get; init; }
}

public sealed record WorkspaceActivityResponse(Guid Id, string EventType, DateTimeOffset CreatedAt);

public sealed class WorkspaceActivityQuery : PageQuery
{
    [StringLength(64)]
    public string? EventType { get; init; }
}

public sealed record WorkspaceSettingsResponse(
    Guid ProjectId,
    bool MembersCanCreateTasks,
    bool MembersCanScheduleMeetings,
    string? WorkingAgreement,
    DateTimeOffset? UpdatedAt);

public sealed class UpdateWorkspaceSettingsRequest
{
    public bool MembersCanCreateTasks { get; init; } = true;
    public bool MembersCanScheduleMeetings { get; init; } = true;

    [StringLength(2000)]
    public string? WorkingAgreement { get; init; }
}
