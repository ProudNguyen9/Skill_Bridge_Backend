namespace DNTU.SkillBridge.Application.Analytics;

public interface IAnalyticsService
{
    Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SkillAnalyticsResponse>> GetSkillsAsync(CancellationToken cancellationToken);
}

/// <summary>Read-only, privacy-safe aggregate skill supply and demand analytics.</summary>
public sealed class AnalyticsService(IAnalyticsRepository analyticsRepository) : IAnalyticsService
{
    public Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken) =>
        analyticsRepository.GetOverviewAsync(cancellationToken);

    public Task<IReadOnlyCollection<SkillAnalyticsResponse>> GetSkillsAsync(CancellationToken cancellationToken) =>
        analyticsRepository.GetSkillGapRowsAsync(cancellationToken);
}
