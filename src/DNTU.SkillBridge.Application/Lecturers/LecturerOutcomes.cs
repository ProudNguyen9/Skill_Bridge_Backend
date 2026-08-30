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
