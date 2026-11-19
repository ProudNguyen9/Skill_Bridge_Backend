namespace DNTU.SkillBridge.Application.Lecturers;

public enum LecturerAssignmentCreateOutcome
{
    Created,
    LecturerNotFound,
    LecturerInactive,
    DuplicateAssignment
}

public enum LecturerAssignmentAcceptOutcome
{
    Accepted,
    NotFound,
    NotOpenForAcceptance
}

public enum ProjectLecturerInviteOutcome
{
    Created,
    ProjectNotFound,
    Forbidden,
    LecturerNotFound,
    LecturerInactive,
    DuplicateAssignment
}

public enum LecturerRecommendationCreateOutcome
{
    Created,
    LecturerNotApproved,
    TeamNotFound,
    ProjectNotFound,
    DuplicatePending
}
