using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Academics;

public sealed class AcademicEvaluationService(AppDbContext dbContext)
{
    private const decimal PassScore = 50m;

    public async Task<IReadOnlyCollection<AcademicEvaluationResponse>> ListForLecturerAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        await dbContext.AcademicEvaluations.AsNoTracking()
            .Where(item => item.EvaluatorUserId == lecturerUserId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => Map(item))
            .ToListAsync(cancellationToken);

    public async Task<AcademicEvaluationResponse?> GetAsync(Guid requesterUserId, Guid projectId, Guid studentId, CancellationToken cancellationToken)
    {
        if (!await CanReadEvaluationAsync(requesterUserId, projectId, studentId, cancellationToken)) return null;
        var evaluation = await dbContext.AcademicEvaluations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);
        return evaluation is null ? null : Map(evaluation);
    }

    public async Task<AcademicEvaluationResponse?> UpsertAsync(
        Guid lecturerUserId,
        Guid projectId,
        Guid studentId,
        UpsertAcademicEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var authorized = await dbContext.LecturerAssignments
            .Join(dbContext.LecturerProfiles,
                assignment => assignment.LecturerId,
                profile => profile.Id,
                (assignment, profile) => new { assignment, profile })
            .AnyAsync(row => row.assignment.ProjectId == projectId &&
                             row.assignment.Status == LecturerAssignmentStatus.ACTIVE &&
                             row.profile.IsActive &&
                             row.profile.UserId == lecturerUserId,
                cancellationToken);
        if (!authorized || !await dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.StudentId == studentId && member.IsActive, cancellationToken))
        {
            return null;
        }

        var rubric = await dbContext.Rubrics.Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Id == request.RubricId && item.IsLocked, cancellationToken);
        if (rubric is null || !rubric.HasCompleteWeights() || request.Scores.Count != rubric.Criteria.Count || request.Scores.Select(score => score.RubricCriterionId).Distinct().Count() != request.Scores.Count)
        {
            return null;
        }

        var approvedMapping = await dbContext.CourseProjects.AnyAsync(mapping =>
            mapping.ProjectId == projectId &&
            mapping.RubricId == rubric.Id &&
            mapping.Status == CourseProjectStatus.APPROVED,
            cancellationToken);
        if (!approvedMapping)
        {
            return null;
        }

        var criteriaById = rubric.Criteria.ToDictionary(item => item.Id);
        if (request.Scores.Any(score => !criteriaById.ContainsKey(score.RubricCriterionId)))
        {
            return null;
        }

        var evaluation = await dbContext.AcademicEvaluations
            .Include(item => item.Scores)
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId && item.RubricId == rubric.Id, cancellationToken);
        if (evaluation is null)
        {
            evaluation = new AcademicEvaluation(projectId, studentId, rubric.Id, lecturerUserId);
            dbContext.AcademicEvaluations.Add(evaluation);
        }

        try
        {
            var scores = request.Scores.Select(score => new AcademicCriterionScore(
                evaluation.Id,
                score.RubricCriterionId,
                score.Score,
                criteriaById[score.RubricCriterionId].Weight));
            evaluation.ReplaceScores(scores, request.Version, PassScore);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return Map(evaluation);
    }

    public async Task<AcademicEvaluationResponse?> FinalizeAsync(Guid lecturerUserId, Guid projectId, Guid studentId, CancellationToken cancellationToken)
    {
        var evaluation = await dbContext.AcademicEvaluations.SingleOrDefaultAsync(item => item.ProjectId == projectId && item.StudentId == studentId, cancellationToken);
        if (evaluation is null || evaluation.EvaluatorUserId != lecturerUserId)
        {
            return null;
        }

        var hasAcceptedSubmission = await dbContext.ProjectSubmissions.AsNoTracking()
            .AnyAsync(submission => submission.ProjectId == projectId && submission.Status == Domain.Submissions.SubmissionStatus.BUSINESS_ACCEPTED, cancellationToken);
        if (!hasAcceptedSubmission)
        {
            return null;
        }

        try { evaluation.FinalizeEvaluation(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return null; }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return Map(evaluation);
    }

    private async Task<bool> CanReadEvaluationAsync(Guid userId, Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers.AsNoTracking().AnyAsync(member => member.ProjectId == projectId && member.StudentId == studentId && member.IsActive && member.Student.UserId == userId, cancellationToken) ||
        await dbContext.LecturerAssignments.AsNoTracking()
            .Join(dbContext.LecturerProfiles.AsNoTracking(), assignment => assignment.LecturerId, profile => profile.Id, (assignment, profile) => new { assignment, profile })
            .AnyAsync(row => row.assignment.ProjectId == projectId && row.assignment.Status == LecturerAssignmentStatus.ACTIVE && row.profile.UserId == userId && row.profile.IsActive, cancellationToken);

    private static AcademicEvaluationResponse Map(AcademicEvaluation evaluation) => new(
        evaluation.Id,
        evaluation.ProjectId,
        evaluation.StudentId,
        evaluation.RubricId,
        evaluation.TotalScore,
        evaluation.IsPassed,
        evaluation.Status.ToString(),
        evaluation.Version,
        evaluation.FinalizedAt);
}
