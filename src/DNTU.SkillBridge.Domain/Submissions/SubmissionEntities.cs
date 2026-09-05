using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Submissions;

/// <summary>Append-only audit entry for a submission workflow transition.</summary>
public sealed class SubmissionStatusHistory : AuditableEntity
{
    public Guid SubmissionId { get; private set; }
    public SubmissionStatus FromStatus { get; private set; }
    public SubmissionStatus ToStatus { get; private set; }
    public Guid ActorUserId { get; private set; }

    private SubmissionStatusHistory() { }

    public SubmissionStatusHistory(Guid submissionId, SubmissionStatus fromStatus, SubmissionStatus toStatus, Guid actorUserId)
    {
        SubmissionId = submissionId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorUserId = actorUserId;
    }
}

public sealed class ProjectSubmission : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid? MilestoneId { get; private set; }
    public Guid SubmittedByStudentId { get; private set; }
    public SubmissionStatus Status { get; private set; } = SubmissionStatus.SUBMITTED;
    public int CurrentVersionNumber { get; private set; } = 1;
    public Guid Version { get; private set; } = Guid.CreateVersion7();
    public ICollection<SubmissionVersion> Versions { get; } = [];

    private ProjectSubmission() { }

    public ProjectSubmission(Guid projectId, Guid? milestoneId, Guid submittedByStudentId, SubmissionVersion initialVersion)
    {
        ProjectId = projectId;
        MilestoneId = milestoneId;
        SubmittedByStudentId = submittedByStudentId;
        initialVersion.AttachToSubmission(Id, 1);
        Versions.Add(initialVersion);
    }

    /// <summary>Adds an immutable resubmission after optimistic-concurrency validation.</summary>
    public void AddVersion(SubmissionVersion version, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status is not (SubmissionStatus.SUBMITTED or SubmissionStatus.REVISION_REQUIRED or SubmissionStatus.RESUBMITTED))
        {
            throw new InvalidOperationException("This submission cannot receive another version.");
        }

        var nextNumber = CurrentVersionNumber + 1;
        version.AttachToSubmission(Id, nextNumber);
        CurrentVersionNumber = nextNumber;
        Status = SubmissionStatus.RESUBMITTED;
        Version = Guid.CreateVersion7();
        Versions.Add(version);
    }

    public void RequireRevision(Guid expectedVersion) => Transition(
        SubmissionStatus.REVISION_REQUIRED,
        expectedVersion,
        SubmissionStatus.SUBMITTED,
        SubmissionStatus.RESUBMITTED,
        SubmissionStatus.TECHNICAL_APPROVED);

    public void ApproveTechnical(Guid expectedVersion) => Transition(
        SubmissionStatus.TECHNICAL_APPROVED,
        expectedVersion,
        SubmissionStatus.SUBMITTED,
        SubmissionStatus.RESUBMITTED);

    public void AcceptBusiness(Guid expectedVersion) => Transition(
        SubmissionStatus.BUSINESS_ACCEPTED,
        expectedVersion,
        SubmissionStatus.TECHNICAL_APPROVED);

    private void Transition(SubmissionStatus target, Guid expectedVersion, params SubmissionStatus[] allowedFrom)
    {
        EnsureVersion(expectedVersion);
        if (!allowedFrom.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot transition a submission from {Status} to {target}.");
        }

        Status = target;
        Version = Guid.CreateVersion7();
    }

    private void EnsureVersion(Guid expectedVersion)
    {
        if (expectedVersion == Guid.Empty || Version != expectedVersion)
        {
            throw new InvalidOperationException("The submission was changed by another request.");
        }
    }
}

/// <summary>Immutable evidence snapshot. Versions may only be created, never edited or removed.</summary>
public sealed class SubmissionVersion : AuditableEntity
{
    public Guid SubmissionId { get; private set; }
    public int VersionNumber { get; private set; }
    public string? Summary { get; private set; }
    public string? GithubUrl { get; private set; }
    public string? DemoUrl { get; private set; }
    public string? VideoUrl { get; private set; }
    public Guid? FileId { get; private set; }

    private SubmissionVersion() { }

    public SubmissionVersion(string? summary, string? githubUrl, string? demoUrl, string? videoUrl, Guid? fileId)
    {
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        GithubUrl = NormalizeUrl(githubUrl);
        DemoUrl = NormalizeUrl(demoUrl);
        VideoUrl = NormalizeUrl(videoUrl);
        FileId = fileId;

        if (Summary is null && GithubUrl is null && DemoUrl is null && VideoUrl is null && FileId is null)
        {
            throw new ArgumentException("At least one evidence item is required.");
        }
    }

    internal void AttachToSubmission(Guid submissionId, int versionNumber)
    {
        if (submissionId == Guid.Empty || versionNumber <= 0 || SubmissionId != Guid.Empty || VersionNumber != 0)
        {
            throw new InvalidOperationException("A submission version is immutable after it is attached.");
        }

        SubmissionId = submissionId;
        VersionNumber = versionNumber;
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("Evidence URL must be absolute HTTP(S).", nameof(value));
        }

        return uri.ToString();
    }
}
