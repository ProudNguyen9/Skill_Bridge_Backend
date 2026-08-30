using DNTU.SkillBridge.Application.Lecturers;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the lecturer profile and supervision assignment data operations.</summary>
public sealed class LecturerRepository(AppDbContext dbContext) : ILecturerRepository
{
    public Task<LecturerProfile?> FindProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles
            .AsTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

    public Task<LecturerProfile?> FindProfileForUpdateAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == lecturerId, cancellationToken);

    public void AddProfile(LecturerProfile profile) => dbContext.LecturerProfiles.Add(profile);

    public void AddAssignment(LecturerAssignment assignment) => dbContext.LecturerAssignments.Add(assignment);

    public Task<LecturerAssignment?> FindAssignmentForUpdateAsync(Guid assignmentId, Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == assignmentId && item.LecturerId == lecturerId, cancellationToken);

    public Task<LecturerAssignment?> FindAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);

    public void RemoveAssignment(LecturerAssignment assignment) => dbContext.LecturerAssignments.Remove(assignment);

    public Task<bool> HasAssignmentForProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.LecturerAssignments
            .AnyAsync(item => item.ProjectId == projectId, cancellationToken);

    public async Task<IReadOnlyCollection<LecturerAssignment>> ListAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        await dbContext.LecturerAssignments
            .AsNoTracking()
            .Where(item => item.LecturerId == lecturerId)
            .OrderBy(item => item.AssignedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
}
