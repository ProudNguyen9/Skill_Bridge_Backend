using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Portfolio;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Students;

public sealed class PortfolioService(AppDbContext dbContext)
{
    public async Task<IReadOnlyCollection<VerifiedSkillResponse>> ListVerifiedSkillsAsync(Guid studentId, bool includeRevoked, CancellationToken cancellationToken)
    {
        var query = dbContext.VerifiedSkills.AsNoTracking().Where(item => item.StudentId == studentId);
        if (!includeRevoked) query = query.Where(item => !item.IsRevoked);
        return await ProjectVerifiedSkillsAsync(query, cancellationToken);
    }

    public async Task<VerifiedSkillResponse?> VerifySkillAsync(Guid lecturerUserId, Guid studentId, Guid skillId, VerifySkillRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAssignedLecturerAsync(lecturerUserId, request.ProjectId, cancellationToken)) return null;
        if (!await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == request.ProjectId && member.StudentId == studentId && member.IsActive, cancellationToken)) return null;
        if (!await dbContext.ProjectSkills.AnyAsync(skill => skill.ProjectId == request.ProjectId && skill.SkillId == skillId, cancellationToken)) return null;

        var evaluation = await dbContext.AcademicEvaluations.AsNoTracking()
            .Where(item => item.ProjectId == request.ProjectId &&
                           item.StudentId == studentId &&
                           item.Status == AcademicEvaluationStatus.FINALIZED &&
                           item.IsPassed)
            .OrderByDescending(item => item.FinalizedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (evaluation is null) return null;

        if (request.EvidenceSubmissionId.HasValue &&
            !await dbContext.ProjectSubmissions.AsNoTracking().AnyAsync(item =>
                item.Id == request.EvidenceSubmissionId.Value &&
                item.ProjectId == request.ProjectId &&
                item.Status == SubmissionStatus.BUSINESS_ACCEPTED,
                cancellationToken))
        {
            return null;
        }

        var verified = await dbContext.VerifiedSkills.AsTracking()
            .SingleOrDefaultAsync(item => item.StudentId == studentId && item.SkillId == skillId && item.ProjectId == request.ProjectId, cancellationToken);
        if (verified is null)
        {
            verified = new VerifiedSkill(studentId, skillId, request.ProjectId, lecturerUserId, request.Level, request.EvidenceSubmissionId, evaluation.Id);
            dbContext.VerifiedSkills.Add(verified);
        }
        else
        {
            verified.Restore(lecturerUserId, request.Level, request.EvidenceSubmissionId, evaluation.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ProjectVerifiedSkillsAsync(dbContext.VerifiedSkills.AsNoTracking().Where(item => item.Id == verified.Id), cancellationToken)).Single();
    }

    public async Task<VerifiedSkillResponse?> RevokeSkillAsync(Guid lecturerUserId, Guid studentId, Guid skillId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await IsAssignedLecturerAsync(lecturerUserId, projectId, cancellationToken)) return null;
        var verified = await dbContext.VerifiedSkills.AsTracking()
            .SingleOrDefaultAsync(item => item.StudentId == studentId && item.SkillId == skillId && item.ProjectId == projectId, cancellationToken);
        if (verified is null) return null;
        verified.Revoke(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ProjectVerifiedSkillsAsync(dbContext.VerifiedSkills.AsNoTracking().Where(item => item.Id == verified.Id), cancellationToken)).Single();
    }

    public async Task<IReadOnlyCollection<PortfolioEntryResponse>> ListPortfolioAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.PortfolioEntries.AsNoTracking().Where(item => item.StudentId == studentId);
        if (publicOnly) query = query.Where(item => item.IsPublished);
        return await ProjectPortfolioAsync(query, cancellationToken);
    }

    public async Task<PortfolioEntryResponse?> UpsertPortfolioEntryAsync(Guid userId, Guid projectId, UpsertPortfolioEntryRequest request, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdForUserAsync(userId, cancellationToken);
        if (!studentId.HasValue || !await CanPortfolioProjectAsync(studentId.Value, projectId, requireCompleted: false, cancellationToken)) return null;

        var entry = await dbContext.PortfolioEntries.AsTracking()
            .SingleOrDefaultAsync(item => item.StudentId == studentId.Value && item.ProjectId == projectId, cancellationToken);
        if (entry is null)
        {
            entry = new PortfolioEntry(studentId.Value, projectId, request.Summary);
            dbContext.PortfolioEntries.Add(entry);
        }
        else
        {
            entry.Update(request.Summary);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ProjectPortfolioAsync(dbContext.PortfolioEntries.AsNoTracking().Where(item => item.Id == entry.Id), cancellationToken)).Single();
    }

    public async Task<PortfolioEntryResponse?> SetPublishedAsync(Guid userId, Guid projectId, bool published, CancellationToken cancellationToken)
    {
        var studentId = await StudentIdForUserAsync(userId, cancellationToken);
        if (!studentId.HasValue || !await CanPortfolioProjectAsync(studentId.Value, projectId, requireCompleted: true, cancellationToken)) return null;

        var entry = await dbContext.PortfolioEntries.AsTracking()
            .SingleOrDefaultAsync(item => item.StudentId == studentId.Value && item.ProjectId == projectId, cancellationToken);
        if (entry is null) return null;
        if (published) entry.Publish(); else entry.Unpublish();
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ProjectPortfolioAsync(dbContext.PortfolioEntries.AsNoTracking().Where(item => item.Id == entry.Id), cancellationToken)).Single();
    }

    public async Task<SkillPassportResponse?> GetPassportAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken)
    {
        if (publicOnly)
        {
            var isPublic = await dbContext.StudentProfiles.AsNoTracking()
                .AnyAsync(profile => profile.Id == studentId && profile.Privacy.IsProfilePublic, cancellationToken);
            if (!isPublic) return null;
        }

        var skills = await ListVerifiedSkillsAsync(studentId, includeRevoked: !publicOnly, cancellationToken);
        var portfolio = await ListPortfolioAsync(studentId, publicOnly, cancellationToken);
        return new SkillPassportResponse(studentId, skills, portfolio);
    }

    private Task<Guid?> StudentIdForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> CanPortfolioProjectAsync(Guid studentId, Guid projectId, bool requireCompleted, CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers.AsNoTracking()
            .AnyAsync(item => item.ProjectId == projectId && item.StudentId == studentId && item.IsActive, cancellationToken);
        if (!member) return false;
        if (!requireCompleted) return true;
        return await dbContext.Projects.AsNoTracking().AnyAsync(item => item.Id == projectId && item.Status == ProjectStatus.COMPLETED, cancellationToken);
    }

    private async Task<bool> IsAssignedLecturerAsync(Guid lecturerUserId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.LecturerAssignments.AsNoTracking()
            .Join(dbContext.LecturerProfiles.AsNoTracking(), assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId &&
                             row.assignment.Status == LecturerAssignmentStatus.ACTIVE &&
                             row.lecturer.IsActive &&
                             row.lecturer.UserId == lecturerUserId,
                cancellationToken);

    private async Task<IReadOnlyCollection<VerifiedSkillResponse>> ProjectVerifiedSkillsAsync(IQueryable<VerifiedSkill> query, CancellationToken cancellationToken)
    {
        var rows = await query
            .Join(dbContext.Skills.AsNoTracking(), verified => verified.SkillId, skill => skill.Id, (verified, skill) => new { verified, skill.Name })
            .Join(dbContext.Projects.AsNoTracking(), row => row.verified.ProjectId, project => project.Id, (row, project) => new { row.verified, SkillName = row.Name, ProjectTitle = project.Title })
            .OrderBy(row => row.SkillName)
            .ThenBy(row => row.ProjectTitle)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new VerifiedSkillResponse(
            row.verified.Id,
            row.verified.StudentId,
            row.verified.SkillId,
            row.SkillName,
            row.verified.ProjectId,
            row.ProjectTitle,
            row.verified.Level,
            row.verified.IsRevoked,
            row.verified.VerifiedAt,
            row.verified.RevokedAt,
            row.verified.EvidenceSubmissionId,
            row.verified.EvidenceEvaluationId)).ToList();
    }

    private async Task<IReadOnlyCollection<PortfolioEntryResponse>> ProjectPortfolioAsync(IQueryable<PortfolioEntry> query, CancellationToken cancellationToken)
    {
        var rows = await query
            .Join(dbContext.Projects.AsNoTracking(), entry => entry.ProjectId, project => project.Id, (entry, project) => new { entry, project.Title })
            .OrderBy(row => row.Title)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new PortfolioEntryResponse(row.entry.Id, row.entry.StudentId, row.entry.ProjectId, row.Title, row.entry.Summary, row.entry.IsPublished)).ToList();
    }
}
