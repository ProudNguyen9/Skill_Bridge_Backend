using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.UnitTests;

public sealed class SubmissionEntitiesTests
{
    [Fact]
    public void Version_requires_at_least_one_evidence_item()
    {
        Assert.Throws<ArgumentException>(() => new SubmissionVersion(null, null, null, null, null));
    }

    [Fact]
    public void New_version_is_immutable_and_rejects_stale_concurrency_token()
    {
        var submission = new ProjectSubmission(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            new SubmissionVersion("Bản đầu", "https://github.com/dntu/skillbridge", null, null, null));
        var initialToken = submission.Version;

        submission.AddVersion(new SubmissionVersion("Bản sửa", null, "https://demo.skillbridge.local", null, null), initialToken);

        Assert.Equal(2, submission.CurrentVersionNumber);
        Assert.Equal(SubmissionStatus.RESUBMITTED, submission.Status);
        Assert.Equal(2, submission.Versions.Single(version => version.VersionNumber == 2).VersionNumber);
        Assert.Throws<InvalidOperationException>(() => submission.AddVersion(
            new SubmissionVersion("Bản trễ", null, null, "https://video.skillbridge.local", null),
            initialToken));
        Assert.Equal(2, submission.CurrentVersionNumber);
    }

    [Fact]
    public void Approved_submission_cannot_receive_another_version()
    {
        var submission = new ProjectSubmission(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            new SubmissionVersion("Bản đầu", null, "https://demo.skillbridge.local", null, null));
        submission.ApproveTechnical(submission.Version);

        Assert.Throws<InvalidOperationException>(() => submission.AddVersion(
            new SubmissionVersion("Bản không hợp lệ", null, null, null, Guid.NewGuid()),
            submission.Version));
    }
}
