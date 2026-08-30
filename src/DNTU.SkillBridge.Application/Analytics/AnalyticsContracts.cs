namespace DNTU.SkillBridge.Application.Analytics;

public sealed record SkillAnalyticsResponse(Guid SkillId, string SkillName, int Demand, int DeclaredSupply, int VerifiedSupply, int Gap);
public sealed record AnalyticsOverviewResponse(int ActiveProjects, int StudentsWithDeclaredSkills, int VerifiedSkills, int ProjectsAtRisk);
