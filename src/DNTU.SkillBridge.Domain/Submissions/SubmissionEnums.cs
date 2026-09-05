namespace DNTU.SkillBridge.Domain.Submissions;

public enum SubmissionStatus
{
    SUBMITTED = 1,
    REVISION_REQUIRED = 2,
    RESUBMITTED = 3,
    TECHNICAL_APPROVED = 4,
    BUSINESS_ACCEPTED = 5
}

public enum BusinessReviewDecision
{
    ACCEPTED = 1,
    REVISION_REQUIRED = 2
}

public enum TechnicalReviewDecision
{
    APPROVED = 1,
    REVISION_REQUIRED = 2
}
