using System.Security.Claims;
using DNTU.SkillBridge.Application.Common.Security;

namespace DNTU.SkillBridge.Api.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid? UserId => GetGuid(ClaimTypes.NameIdentifier);
    public Guid? SessionId => GetGuid("session_id");
    public Guid? StudentId => GetGuid("student_id");
    public Guid? CompanyId => GetGuid("company_id");
    public Guid? LecturerId => GetGuid("lecturer_id");
    public IReadOnlySet<string> Roles => GetValues(ClaimTypes.Role);
    public IReadOnlySet<string> Permissions => GetValues("permission");

    private Guid? GetGuid(string type) => Guid.TryParse(Principal?.FindFirstValue(type), out var value) ? value : null;

    private IReadOnlySet<string> GetValues(string type) => Principal?
        .FindAll(type)
        .Select(claim => claim.Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
