namespace DNTU.SkillBridge.Application.Analytics;

/// <summary>Read-side aggregate queries backing the admin analytics endpoints.</summary>
public interface IAnalyticsRepository
{
    Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SkillAnalyticsResponse>> GetSkillGapRowsAsync(CancellationToken cancellationToken);
}
