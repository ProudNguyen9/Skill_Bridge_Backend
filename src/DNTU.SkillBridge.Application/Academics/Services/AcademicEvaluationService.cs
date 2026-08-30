using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Academics;

public sealed class AcademicEvaluationService(IAcademicEvaluationRepository evaluationRepository) : IAcademicEvaluationService
{
    private const decimal PassScore = 50m;

    public async Task<IReadOnlyCollection<AcademicEvaluationResponse>> ListForLecturerAsync(Guid lecturerUserId, CancellationToken cancellationToken) =>
        (await evaluationRepository.ListForLecturerAsync(lecturerUserId, cancellationToken))
            .Select(Map)
            .ToList();

    public async Task<AcademicEvaluationResponse?> GetAsync(Guid requesterUserId, Guid projectId, Guid studentId, CancellationToken cancellationToken)
    {
        if (!await CanReadEvaluationAsync(requesterUserId, projectId, studentId, cancellationToken)) return null;
        var evaluation = await evaluationRepository.FindEvaluationAsync(projectId, studentId, cancellationToken);
        return evaluation is null ? null : Map(evaluation);
    }

    public async Task<AcademicEvaluationResponse?> UpsertAsync(
        Guid lecturerUserId,
        Guid projectId,
        Guid studentId,
        UpsertAcademicEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var authorized = await evaluationRepository.IsAssignedActiveLecturerAsync(lecturerUserId, projectId, cancellationToken);
        if (!authorized || !await evaluationRepository.HasActiveProjectMemberAsync(projectId, studentId, cancellationToken))
        {
            return null;
        }

        var rubric = await evaluationRepository.FindLockedRubricWithCriteriaAsync(request.RubricId, cancellationToken);
        if (rubric is null || !rubric.HasCompleteWeights() || request.Scores.Count != rubric.Criteria.Count || request.Scores.Select(score => score.RubricCriterionId).Distinct().Count() != request.Scores.Count)
        {
            return null;
        }

        if (!await evaluationRepository.HasApprovedCourseProjectAsync(projectId, rubric.Id, cancellationToken))
        {
            return null;
        }

        var criteriaById = rubric.Criteria.ToDictionary(item => item.Id);
        if (request.Scores.Any(score => !criteriaById.ContainsKey(score.RubricCriterionId)))
        {
            return null;
        }

        var evaluation = await evaluationRepository.FindEvaluationWithScoresAsync(projectId, studentId, rubric.Id, cancellationToken);
        if (evaluation is null)
        {
            evaluation = new AcademicEvaluation(projectId, studentId, rubric.Id, lecturerUserId);
            evaluationRepository.AddEvaluation(evaluation);
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

        if (!await evaluationRepository.TrySaveChangesAsync(cancellationToken))
        {
            return null;
        }

        return Map(evaluation);
    }

    public async Task<AcademicEvaluationResponse?> FinalizeAsync(Guid lecturerUserId, Guid projectId, Guid studentId, CancellationToken cancellationToken)
    {
        var evaluation = await evaluationRepository.FindEvaluationForUpdateAsync(projectId, studentId, cancellationToken);
        if (evaluation is null || evaluation.EvaluatorUserId != lecturerUserId)
        {
            return null;
        }

        if (!await evaluationRepository.HasAcceptedSubmissionAsync(projectId, cancellationToken))
        {
            return null;
        }

        try { evaluation.FinalizeEvaluation(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return null; }
        if (!await evaluationRepository.TrySaveChangesAsync(cancellationToken))
        {
            return null;
        }

        return Map(evaluation);
    }

    private async Task<bool> CanReadEvaluationAsync(Guid userId, Guid projectId, Guid studentId, CancellationToken cancellationToken) =>
        await evaluationRepository.HasActiveMemberForUserAsync(projectId, studentId, userId, cancellationToken) ||
        await evaluationRepository.CanLecturerReadProjectAsync(userId, projectId, cancellationToken);

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
