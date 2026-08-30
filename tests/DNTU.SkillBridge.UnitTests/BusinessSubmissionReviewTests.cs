using DNTU.SkillBridge.Domain.Submissions;

namespace DNTU.SkillBridge.UnitTests;

public sealed class BusinessSubmissionReviewTests
{
    [Fact]
    public void Review_captures_business_only_feedback_without_academic_fields()
    {
        var review = new BusinessSubmissionReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            BusinessReviewDecision.ACCEPTED,
            "Đáp ứng các yêu cầu nghiệp vụ đã thống nhất.",
            "Phối hợp và bàn giao đúng hạn.");

        Assert.Equal(BusinessReviewDecision.ACCEPTED, review.Decision);
        Assert.Equal("Đáp ứng các yêu cầu nghiệp vụ đã thống nhất.", review.RequirementsFeedback);
        Assert.Equal("Phối hợp và bàn giao đúng hạn.", review.CollaborationFeedback);
        Assert.DoesNotContain(typeof(BusinessSubmissionReview).GetProperties(), property =>
            property.Name.Contains("Score", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Academic", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Skill", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Revision_requires_a_business_rationale_and_keeps_submission_eligible_for_a_new_version()
    {
        Assert.Throws<ArgumentException>(() => new BusinessSubmissionReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            BusinessReviewDecision.REVISION_REQUIRED,
            "Cần rà soát lại tính đầy đủ của yêu cầu.",
            " "));

        var submission = new ProjectSubmission(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            new SubmissionVersion("Bản đầu", null, null, null, Guid.NewGuid()));
        submission.ApproveTechnical(submission.Version);
        submission.RequireRevision(submission.Version);
        submission.AddVersion(new SubmissionVersion("Bản cập nhật", null, null, null, Guid.NewGuid()), submission.Version);

        Assert.Equal(SubmissionStatus.RESUBMITTED, submission.Status);
        Assert.Equal(2, submission.CurrentVersionNumber);
    }

    [Fact]
    public void Business_acceptance_is_not_project_completion()
    {
        var submission = new ProjectSubmission(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            new SubmissionVersion("Bản đầu", null, null, null, Guid.NewGuid()));
        submission.ApproveTechnical(submission.Version);
        submission.AcceptBusiness(submission.Version);

        Assert.Equal(SubmissionStatus.BUSINESS_ACCEPTED, submission.Status);
        Assert.NotEqual(SubmissionStatus.TECHNICAL_APPROVED, submission.Status);
    }
}
