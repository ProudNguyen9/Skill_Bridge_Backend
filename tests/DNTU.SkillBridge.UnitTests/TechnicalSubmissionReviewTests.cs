using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.UnitTests;

public sealed class TechnicalSubmissionReviewTests
{
    [Fact]
    public void Review_captures_immutable_rubric_ready_notes()
    {
        var review = new TechnicalSubmissionReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TechnicalReviewDecision.REVISION_REQUIRED,
            "Thiếu kiểm thử biên.",
            "{\"criteria\":[{\"name\":\"Tests\",\"note\":\"Bổ sung edge cases\"}]}");

        Assert.Equal(TechnicalReviewDecision.REVISION_REQUIRED, review.Decision);
        Assert.Equal("Thiếu kiểm thử biên.", review.Feedback);
        Assert.Contains("edge cases", review.CriteriaNotes, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Review_rejects_unknown_decision(int rawDecision)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TechnicalSubmissionReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            (TechnicalReviewDecision)rawDecision,
            "Phản hồi hợp lệ.",
            null));
    }

    [Fact]
    public void Revision_request_keeps_submission_eligible_for_new_evidence_version()
    {
        var submission = new ProjectSubmission(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            new SubmissionVersion("Bản đầu", null, null, null, Guid.NewGuid()));

        submission.RequireRevision(submission.Version);
        submission.AddVersion(new SubmissionVersion("Bản sửa", null, null, null, Guid.NewGuid()), submission.Version);

        Assert.Equal(SubmissionStatus.RESUBMITTED, submission.Status);
        Assert.Equal(2, submission.CurrentVersionNumber);
    }
}
