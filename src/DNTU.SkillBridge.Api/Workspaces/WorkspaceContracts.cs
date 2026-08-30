using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Api.Workspaces;

public sealed record WorkspaceOverviewResponse(
    Guid ProjectId,
    string Title,
    string Slug,
    string Status,
    int MemberCount,
    int OpenTaskCount,
    int OverdueTaskCount,
    IReadOnlySet<string> Capabilities);

public sealed record WorkspaceMemberResponse(Guid StudentId, string DisplayName, DateTimeOffset JoinedAt);
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
