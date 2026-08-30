using DNTU.SkillBridge.Domain.Workspaces;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Analytics;

public sealed record SkillAnalyticsResponse(Guid SkillId, string SkillName, int Demand, int DeclaredSupply, int VerifiedSupply, int Gap);
public sealed record AnalyticsOverviewResponse(int ActiveProjects, int StudentsWithDeclaredSkills, int VerifiedSkills, int ProjectsAtRisk);

/// <summary>Read-only, privacy-safe aggregate skill supply and demand analytics.</summary>
public sealed class AnalyticsService(AppDbContext dbContext)
{
    public async Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken) => new(
        await dbContext.Projects.CountAsync(item => item.IsActive, cancellationToken),
        await dbContext.StudentSkills.Select(item => item.StudentId).Distinct().CountAsync(cancellationToken),
        await dbContext.VerifiedSkills.CountAsync(item => !item.IsRevoked, cancellationToken),
        await dbContext.ProjectRiskSnapshots.CountAsync(item => item.Level != ProjectRiskLevel.LOW, cancellationToken));

    public async Task<IReadOnlyCollection<SkillAnalyticsResponse>> GetSkillsAsync(CancellationToken cancellationToken)
    {
        var demand = await dbContext.ProjectSkills.AsNoTracking()
            .Join(dbContext.Projects.AsNoTracking().Where(project => project.IsActive), skill => skill.ProjectId, project => project.Id, (skill, _) => skill.SkillId)
            .GroupBy(skillId => skillId).Select(group => new { SkillId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.SkillId, item => item.Count, cancellationToken);
        var declared = await dbContext.StudentSkills.AsNoTracking().GroupBy(item => item.SkillId).Select(group => new { SkillId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.SkillId, item => item.Count, cancellationToken);
        var verified = await dbContext.VerifiedSkills.AsNoTracking().Where(item => !item.IsRevoked).GroupBy(item => item.SkillId).Select(group => new { SkillId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.SkillId, item => item.Count, cancellationToken);
        var skillIds = demand.Keys.Union(declared.Keys).Union(verified.Keys).ToArray();
        var names = await dbContext.Skills.AsNoTracking().Where(item => skillIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        return skillIds.OrderBy(id => names[id]).Select(id => new SkillAnalyticsResponse(id, names[id], demand.GetValueOrDefault(id), declared.GetValueOrDefault(id), verified.GetValueOrDefault(id), demand.GetValueOrDefault(id) - verified.GetValueOrDefault(id))).ToList();
    }
}
