using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Portfolio;

public enum SkillVerificationStatus
{
    PENDING = 1,
    APPROVED = 2,
    CHANGES_REQUESTED = 3,
    REJECTED = 4
}

public sealed class SkillVerificationRequest : AuditableEntity
{
    public Guid StudentId { get; private set; }
    public Guid SkillId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? AssignedLecturerId { get; private set; }
    public SkillVerificationStatus Status { get; private set; } = SkillVerificationStatus.PENDING;
    public string Level { get; private set; } = "Cơ bản";
    public string? EvidenceUrl { get; private set; }
    public string? EvidenceFileUrlsJson { get; private set; }
    public string? StudentNote { get; private set; }
    public Guid? ReviewedByLecturerId { get; private set; }
    public string? LecturerFeedback { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; } = DateTimeOffset.UtcNow;

    private SkillVerificationRequest() { }

    public SkillVerificationRequest(
        Guid studentId,
        Guid skillId,
        Guid? projectId,
        Guid? assignedLecturerId,
        string level,
        string? evidenceUrl,
        string? evidenceFileUrlsJson,
        string? studentNote)
    {
        StudentId = studentId;
        SkillId = skillId;
        ProjectId = projectId;
        AssignedLecturerId = assignedLecturerId;
        Level = string.IsNullOrWhiteSpace(level) ? "Level 3 - Nâng cao" : level.Trim();
        EvidenceUrl = string.IsNullOrWhiteSpace(evidenceUrl) ? null : evidenceUrl.Trim();
        EvidenceFileUrlsJson = string.IsNullOrWhiteSpace(evidenceFileUrlsJson) ? null : evidenceFileUrlsJson.Trim();
        StudentNote = string.IsNullOrWhiteSpace(studentNote) ? null : studentNote.Trim();
        Status = SkillVerificationStatus.PENDING;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateEvidence(string level, string? evidenceUrl, string? evidenceFileUrlsJson, string? studentNote)
    {
        Level = string.IsNullOrWhiteSpace(level) ? Level : level.Trim();
        EvidenceUrl = string.IsNullOrWhiteSpace(evidenceUrl) ? null : evidenceUrl.Trim();
        EvidenceFileUrlsJson = string.IsNullOrWhiteSpace(evidenceFileUrlsJson) ? null : evidenceFileUrlsJson.Trim();
        StudentNote = string.IsNullOrWhiteSpace(studentNote) ? null : studentNote.Trim();
        Status = SkillVerificationStatus.PENDING;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    public void Approve(Guid reviewerLecturerId, string? feedback)
    {
        ReviewedByLecturerId = reviewerLecturerId;
        LecturerFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        Status = SkillVerificationStatus.APPROVED;
    }

    public void RequestChanges(Guid reviewerLecturerId, string feedback)
    {
        ReviewedByLecturerId = reviewerLecturerId;
        LecturerFeedback = string.IsNullOrWhiteSpace(feedback) ? "Vui lòng bổ sung thêm minh chứng thẩm định." : feedback.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        Status = SkillVerificationStatus.CHANGES_REQUESTED;
    }

    public void Reject(Guid reviewerLecturerId, string feedback)
    {
        ReviewedByLecturerId = reviewerLecturerId;
        LecturerFeedback = string.IsNullOrWhiteSpace(feedback) ? "Hồ sơ không đáp ứng tiêu chuẩn thẩm định." : feedback.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        Status = SkillVerificationStatus.REJECTED;
    }
}
