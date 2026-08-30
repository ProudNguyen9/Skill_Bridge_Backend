using System.ComponentModel.DataAnnotations;

namespace DNTU.SkillBridge.Application.Submissions;

public class CreateSubmissionRequest
{
    public Guid? MilestoneId { get; init; }
    [StringLength(4000)] public string? Summary { get; init; }
    [Url, StringLength(1000)] public string? GithubUrl { get; init; }
    [Url, StringLength(1000)] public string? DemoUrl { get; init; }
    [Url, StringLength(1000)] public string? VideoUrl { get; init; }
    public Guid? FileId { get; init; }
}

public sealed class CreateSubmissionVersionRequest : CreateSubmissionRequest
{
    [Required] public Guid Version { get; init; }
}

public sealed record SubmissionResponse(
    Guid Id,
    Guid ProjectId,
    Guid? MilestoneId,
    string Status,
    int CurrentVersionNumber,
    Guid SubmittedByStudentId,
    Guid Version,
    DateTimeOffset CreatedAt);

public sealed record SubmissionVersionResponse(
    Guid Id,
    Guid SubmissionId,
    int VersionNumber,
    string? Summary,
    string? GithubUrl,
    string? DemoUrl,
    string? VideoUrl,
    Guid? FileId,
    DateTimeOffset CreatedAt);

public sealed record SubmissionStatusHistoryResponse(
    Guid Id,
    string FromStatus,
    string ToStatus,
    Guid ActorUserId,
    DateTimeOffset CreatedAt);
