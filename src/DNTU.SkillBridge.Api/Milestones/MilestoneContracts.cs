using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Milestones;

namespace DNTU.SkillBridge.Api.Milestones;

public sealed class CreateMilestoneRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    [Range(1, 1000)] public int Sequence { get; init; }
    public DateTimeOffset DueAt { get; init; }
}

public sealed class UpdateMilestoneRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    [Range(1, 1000)] public int Sequence { get; init; }
    public DateTimeOffset DueAt { get; init; }
    public Guid Version { get; init; }
}

public sealed class TransitionMilestoneRequest
{
    public Guid Version { get; init; }
    [StringLength(2000)] public string? Note { get; init; }
}

public sealed class CreateMilestoneDeliverableRequest
{
    [Required, StringLength(300, MinimumLength = 1)] public string Name { get; init; } = string.Empty;
    [StringLength(2000)] public string? Criteria { get; init; }
    public Guid? FileId { get; init; }
}

public sealed class UpdateMilestoneDeliverableRequest
{
    [Required, StringLength(300, MinimumLength = 1)] public string Name { get; init; } = string.Empty;
    [StringLength(2000)] public string? Criteria { get; init; }
    public Guid? FileId { get; init; }
}

public sealed record MilestoneResponse(Guid Id, Guid ProjectId, string Title, string? Description, int Sequence, DateTimeOffset DueAt, MilestoneStatus Status, bool IsOverdue, Guid Version, DateTimeOffset CreatedAt, IReadOnlyCollection<MilestoneDeliverableResponse> Deliverables, IReadOnlyCollection<MilestoneApprovalHistoryResponse> ApprovalHistory);
public sealed record MilestoneDeliverableResponse(Guid Id, Guid MilestoneId, string Name, string? Criteria, Guid? FileId);
public sealed record MilestoneApprovalHistoryResponse(Guid Id, MilestoneStatus FromStatus, MilestoneStatus ToStatus, Guid ActorUserId, string? Note, DateTimeOffset CreatedAt);
