using DNTU.SkillBridge.Application.Students;
using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Portfolio;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the portfolio data operations.</summary>
public sealed class PortfolioRepository(AppDbContext dbContext) : IPortfolioRepository
{
    public async Task<IReadOnlyCollection<VerifiedSkillResponse>> ListVerifiedSkillsAsync(Guid studentId, bool includeRevoked, CancellationToken cancellationToken)
    {
        var query = dbContext.VerifiedSkills.AsNoTracking().Where(item => item.StudentId == studentId);
        if (!includeRevoked) query = query.Where(item => !item.IsRevoked);
        return await ProjectVerifiedSkillsAsync(query, cancellationToken);
    }

    public async Task<VerifiedSkillResponse?> FindVerifiedSkillResponseAsync(Guid verifiedSkillId, CancellationToken cancellationToken)
    {
        var rows = await ProjectVerifiedSkillsAsync(dbContext.VerifiedSkills.AsNoTracking().Where(item => item.Id == verifiedSkillId), cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<bool> IsAssignedLecturerAsync(Guid lecturerUserId, Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.LecturerAssignments.AsNoTracking()
            .Join(dbContext.LecturerProfiles.AsNoTracking(), assignment => assignment.LecturerId, lecturer => lecturer.Id, (assignment, lecturer) => new { assignment, lecturer })
            .AnyAsync(row => row.assignment.ProjectId == projectId &&
                             row.assignment.Status == LecturerAssignmentStatus.ACTIVE &&
                             row.lecturer.IsActive &&
                             row.lecturer.UserId == lecturerUserId,
                cancellationToken);

    public Task<bool> HasActiveProjectMemberAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.StudentId == studentId && member.IsActive, cancellationToken);

    public Task<bool> HasProjectSkillAsync(Guid projectId, Guid skillId, CancellationToken cancellationToken) =>
        dbContext.ProjectSkills.AnyAsync(skill => skill.ProjectId == projectId && skill.SkillId == skillId, cancellationToken);

    public Task<AcademicEvaluation?> FindLatestFinalizedEvaluationAsync(Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        dbContext.AcademicEvaluations.AsNoTracking()
            .Where(item => item.ProjectId == projectId &&
                           item.StudentId == studentId &&
                           item.Status == AcademicEvaluationStatus.FINALIZED &&
                           item.IsPassed)
            .OrderByDescending(item => item.FinalizedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasBusinessAcceptedSubmissionAsync(Guid submissionId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectSubmissions.AsNoTracking().AnyAsync(item =>
            item.Id == submissionId &&
            item.ProjectId == projectId &&
            item.Status == SubmissionStatus.BUSINESS_ACCEPTED,
            cancellationToken);

    /// <summary>Reads a verified skill as a tracked entity so mutations are persisted.</summary>
    public Task<VerifiedSkill?> FindVerifiedSkillForUpdateAsync(Guid studentId, Guid skillId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.VerifiedSkills.AsTracking()
            .SingleOrDefaultAsync(item => item.StudentId == studentId && item.SkillId == skillId && item.ProjectId == projectId, cancellationToken);

    public void AddVerifiedSkill(VerifiedSkill verifiedSkill) => dbContext.VerifiedSkills.Add(verifiedSkill);

    public async Task<IReadOnlyCollection<PortfolioEntryResponse>> ListPortfolioAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.PortfolioEntries.AsNoTracking().Where(item => item.StudentId == studentId);
        if (publicOnly) query = query.Where(item => item.IsPublished);
        return await ProjectPortfolioAsync(query, cancellationToken);
    }

    public async Task<PortfolioEntryResponse?> FindPortfolioEntryResponseAsync(Guid portfolioEntryId, CancellationToken cancellationToken)
    {
        var rows = await ProjectPortfolioAsync(dbContext.PortfolioEntries.AsNoTracking().Where(item => item.Id == portfolioEntryId), cancellationToken);
        return rows.SingleOrDefault();
    }

    public Task<Guid?> FindStudentIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> CanPortfolioProjectAsync(Guid studentId, Guid projectId, bool requireCompleted, CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers.AsNoTracking()
            .AnyAsync(item => item.ProjectId == projectId && item.StudentId == studentId && item.IsActive, cancellationToken);
        if (!member) return false;
        if (!requireCompleted) return true;
        return await dbContext.Projects.AsNoTracking().AnyAsync(item => item.Id == projectId && item.Status == ProjectStatus.COMPLETED, cancellationToken);
    }

    /// <summary>Reads a portfolio entry as a tracked entity so mutations are persisted.</summary>
    public Task<PortfolioEntry?> FindPortfolioEntryForUpdateAsync(Guid studentId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.PortfolioEntries.AsTracking()
            .SingleOrDefaultAsync(item => item.StudentId == studentId && item.ProjectId == projectId, cancellationToken);

    public void AddPortfolioEntry(PortfolioEntry entry) => dbContext.PortfolioEntries.Add(entry);

    public Task<bool> IsProfilePublicAsync(Guid studentId, CancellationToken cancellationToken) =>
        dbContext.StudentProfiles.AsNoTracking()
            .AnyAsync(profile => profile.Id == studentId && profile.Privacy.IsProfilePublic, cancellationToken);

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
