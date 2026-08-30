namespace DNTU.SkillBridge.Application.Common.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? SessionId { get; }
    Guid? StudentId { get; }
    Guid? CompanyId { get; }
    Guid? LecturerId { get; }
    IReadOnlySet<string> Roles { get; }
    IReadOnlySet<string> Permissions { get; }
}
