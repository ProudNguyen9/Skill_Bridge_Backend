using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the project access predicates.</summary>
public sealed class ProjectAccessRepository(AppDbContext dbContext) : IProjectAccessRepository
{
    public Task<bool> HasActiveStudentMembershipAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking()
            .AnyAsync(member => member.ProjectId == projectId
                && member.IsActive
                && member.Student.UserId == userId,
                cancellationToken);

    public Task<bool> HasCompanyAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking()
            .AnyAsync(project => project.Id == projectId
                && project.Company.Members.Any(member => member.UserId == userId),
                cancellationToken);

    public Task<bool> HasLecturerAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments.AsNoTracking()
            .Join(
                dbContext.LecturerProfiles.AsNoTracking(),
                assignment => assignment.LecturerId,
                lecturer => lecturer.Id,
                (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(item => item.assignment.ProjectId == projectId
                && item.assignment.Status == LecturerAssignmentStatus.ACTIVE
                && item.lecturer.UserId == userId,
                cancellationToken);
}
