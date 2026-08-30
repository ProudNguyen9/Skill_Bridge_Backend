using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Portfolio;

namespace DNTU.SkillBridge.Application.Students;

public interface IPortfolioService
{
    Task<IReadOnlyCollection<VerifiedSkillResponse>> ListVerifiedSkillsAsync(Guid studentId, bool includeRevoked, CancellationToken cancellationToken);

    Task<VerifiedSkillResponse?> VerifySkillAsync(Guid lecturerUserId, Guid studentId, Guid skillId, VerifySkillRequest request, CancellationToken cancellationToken);

    Task<VerifiedSkillResponse?> RevokeSkillAsync(Guid lecturerUserId, Guid studentId, Guid skillId, Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PortfolioEntryResponse>> ListPortfolioAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken);

    Task<PortfolioEntryResponse?> UpsertPortfolioEntryAsync(Guid userId, Guid projectId, UpsertPortfolioEntryRequest request, CancellationToken cancellationToken);

    Task<PortfolioEntryResponse?> SetPublishedAsync(Guid userId, Guid projectId, bool published, CancellationToken cancellationToken);

    Task<SkillPassportResponse?> GetPassportAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken);
}

public sealed class PortfolioService(IPortfolioRepository portfolioRepository, IUnitOfWork unitOfWork) : IPortfolioService
{
    public Task<IReadOnlyCollection<VerifiedSkillResponse>> ListVerifiedSkillsAsync(Guid studentId, bool includeRevoked, CancellationToken cancellationToken) =>
        portfolioRepository.ListVerifiedSkillsAsync(studentId, includeRevoked, cancellationToken);

    public async Task<VerifiedSkillResponse?> VerifySkillAsync(Guid lecturerUserId, Guid studentId, Guid skillId, VerifySkillRequest request, CancellationToken cancellationToken)
    {
        if (!await portfolioRepository.IsAssignedLecturerAsync(lecturerUserId, request.ProjectId, cancellationToken)) return null;
        if (!await portfolioRepository.HasActiveProjectMemberAsync(request.ProjectId, studentId, cancellationToken)) return null;
        if (!await portfolioRepository.HasProjectSkillAsync(request.ProjectId, skillId, cancellationToken)) return null;

        var evaluation = await portfolioRepository.FindLatestFinalizedEvaluationAsync(request.ProjectId, studentId, cancellationToken);
        if (evaluation is null) return null;

        if (request.EvidenceSubmissionId.HasValue &&
            !await portfolioRepository.HasBusinessAcceptedSubmissionAsync(request.EvidenceSubmissionId.Value, request.ProjectId, cancellationToken))
        {
            return null;
        }

        var verified = await portfolioRepository.FindVerifiedSkillForUpdateAsync(studentId, skillId, request.ProjectId, cancellationToken);
        if (verified is null)
        {
            verified = new VerifiedSkill(studentId, skillId, request.ProjectId, lecturerUserId, request.Level, request.EvidenceSubmissionId, evaluation.Id);
            portfolioRepository.AddVerifiedSkill(verified);
        }
        else
        {
            verified.Restore(lecturerUserId, request.Level, request.EvidenceSubmissionId, evaluation.Id);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await portfolioRepository.FindVerifiedSkillResponseAsync(verified.Id, cancellationToken);
    }

    public async Task<VerifiedSkillResponse?> RevokeSkillAsync(Guid lecturerUserId, Guid studentId, Guid skillId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!await portfolioRepository.IsAssignedLecturerAsync(lecturerUserId, projectId, cancellationToken)) return null;
        var verified = await portfolioRepository.FindVerifiedSkillForUpdateAsync(studentId, skillId, projectId, cancellationToken);
        if (verified is null) return null;
        verified.Revoke(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await portfolioRepository.FindVerifiedSkillResponseAsync(verified.Id, cancellationToken);
    }

    public Task<IReadOnlyCollection<PortfolioEntryResponse>> ListPortfolioAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken) =>
        portfolioRepository.ListPortfolioAsync(studentId, publicOnly, cancellationToken);

    public async Task<PortfolioEntryResponse?> UpsertPortfolioEntryAsync(Guid userId, Guid projectId, UpsertPortfolioEntryRequest request, CancellationToken cancellationToken)
    {
        var studentId = await portfolioRepository.FindStudentIdAsync(userId, cancellationToken);
        if (!studentId.HasValue || !await portfolioRepository.CanPortfolioProjectAsync(studentId.Value, projectId, requireCompleted: false, cancellationToken)) return null;

        var entry = await portfolioRepository.FindPortfolioEntryForUpdateAsync(studentId.Value, projectId, cancellationToken);
        if (entry is null)
        {
            entry = new PortfolioEntry(studentId.Value, projectId, request.Summary);
            portfolioRepository.AddPortfolioEntry(entry);
        }
        else
        {
            entry.Update(request.Summary);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await portfolioRepository.FindPortfolioEntryResponseAsync(entry.Id, cancellationToken);
    }

    public async Task<PortfolioEntryResponse?> SetPublishedAsync(Guid userId, Guid projectId, bool published, CancellationToken cancellationToken)
    {
        var studentId = await portfolioRepository.FindStudentIdAsync(userId, cancellationToken);
        if (!studentId.HasValue || !await portfolioRepository.CanPortfolioProjectAsync(studentId.Value, projectId, requireCompleted: true, cancellationToken)) return null;

        var entry = await portfolioRepository.FindPortfolioEntryForUpdateAsync(studentId.Value, projectId, cancellationToken);
        if (entry is null) return null;
        if (published) entry.Publish(); else entry.Unpublish();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await portfolioRepository.FindPortfolioEntryResponseAsync(entry.Id, cancellationToken);
    }

    public async Task<SkillPassportResponse?> GetPassportAsync(Guid studentId, bool publicOnly, CancellationToken cancellationToken)
    {
        if (publicOnly)
        {
            var isPublic = await portfolioRepository.IsProfilePublicAsync(studentId, cancellationToken);
            if (!isPublic) return null;
        }

        var skills = await ListVerifiedSkillsAsync(studentId, includeRevoked: !publicOnly, cancellationToken);
        var portfolio = await ListPortfolioAsync(studentId, publicOnly, cancellationToken);
        return new SkillPassportResponse(studentId, skills, portfolio);
    }
}
