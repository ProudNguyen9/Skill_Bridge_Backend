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
