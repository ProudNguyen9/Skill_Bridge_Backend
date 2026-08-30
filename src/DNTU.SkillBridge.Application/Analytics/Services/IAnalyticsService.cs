namespace DNTU.SkillBridge.Application.Analytics;

public interface IAnalyticsService
{
    Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SkillAnalyticsResponse>> GetSkillsAsync(CancellationToken cancellationToken);
}
