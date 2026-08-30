using DNTU.SkillBridge.Application.Files;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

public sealed class FileRepository(AppDbContext dbContext) : IFileRepository
{
    public void Add(FileRecord file) => dbContext.FileRecords.Add(file);

    public Task<FileRecord?> FindOwnedAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        dbContext.FileRecords.SingleOrDefaultAsync(item => item.Id == fileId && item.UploadedByUserId == userId, cancellationToken);

    public Task<FileRecord?> FindAsync(Guid fileId, CancellationToken cancellationToken) =>
        dbContext.FileRecords.SingleOrDefaultAsync(item => item.Id == fileId && item.Status != FileUploadStatus.DELETED, cancellationToken);

    public Task<FileRecord?> FindCompletedAsync(Guid fileId, CancellationToken cancellationToken) =>
        dbContext.FileRecords.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fileId && item.Status == FileUploadStatus.COMPLETED, cancellationToken);

    public async Task<IReadOnlyCollection<FileRecord>> ListExpiredPendingAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.FileRecords
            .Where(file => file.Status == FileUploadStatus.PENDING && file.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasProjectScopeAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.IsActive && member.Student.UserId == userId, cancellationToken) ||
        await dbContext.Projects.AnyAsync(project => project.Id == projectId && project.Company.Members.Any(member => member.UserId == userId), cancellationToken) ||
        await dbContext.LecturerAssignments
            .Join(dbContext.LecturerProfiles,
                assignment => assignment.LecturerId,
                lecturer => lecturer.Id,
                (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId &&
                             row.assignment.Status == DNTU.SkillBridge.Domain.Lecturers.LecturerAssignmentStatus.ACTIVE &&
                             row.lecturer.UserId == userId, cancellationToken);
}
