namespace DNTU.SkillBridge.Domain.Lecturers;

/// <summary>Enum names intentionally match the uppercase wire values (INVITED, ACTIVE, ENDED).</summary>
public enum LecturerAssignmentStatus
{
    INVITED = 1,
    ACTIVE = 2,
    ENDED = 3
}

/// <summary>The supervision role the lecturer holds on a project (primary supervisor vs. co-supervisor).</summary>
public enum LecturerAssignmentRole
{
    PRIMARY = 1,
    SUPERVISOR = 2
}
