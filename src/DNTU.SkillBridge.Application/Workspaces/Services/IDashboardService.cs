using System.Text.Json;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Workspaces;

namespace DNTU.SkillBridge.Application.Workspaces;

public interface IDashboardService
{
    Task<ProjectProgressResponse?> GetProjectProgressAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    Task<StudentDashboardResponse?> GetStudentDashboardAsync(Guid userId, CancellationToken cancellationToken);

    Task<CompanyDashboardResponse?> GetCompanyDashboardAsync(Guid userId, CancellationToken cancellationToken);

    Task<LecturerDashboardResponse?> GetLecturerDashboardAsync(Guid userId, CancellationToken cancellationToken);
}
