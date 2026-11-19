namespace DNTU.SkillBridge.Application.Students;

public sealed record SkillVerificationRequestResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string StudentCode,
    string? StudentAvatarUrl,
    string? StudentClass,
    Guid SkillId,
    string SkillName,
    string SkillCode,
    string SkillCategory,
    string? BadgeImageUrl,
    Guid? ProjectId,
    string? ProjectTitle,
    string? ProjectCode,
    Guid? AssignedLecturerId,
    string? LecturerName,
    string Status,
    string Level,
    string? EvidenceUrl,
    IReadOnlyList<string> EvidenceFileUrls,
    string? StudentNote,
    string? LecturerFeedback,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt);

public sealed record SubmitSkillVerificationRequest(
    Guid SkillId,
    Guid? ProjectId,
    Guid? LecturerId,
    string Level,
    string? EvidenceUrl,
    IReadOnlyList<string>? EvidenceFileUrls,
    string? StudentNote);

public sealed record UpdateSkillVerificationRequest(
    string Level,
    string? EvidenceUrl,
    IReadOnlyList<string>? EvidenceFileUrls,
    string? StudentNote);

public sealed record ReviewSkillVerificationRequest(
    string? Feedback,
    string? Level = null);
