namespace DNTU.SkillBridge.Application.Administration;

public sealed record AdminDashboardResponse(int Companies, int Students, int Lecturers, int Projects, int PendingFundingOrders, int EligibleDisbursements);
public sealed record AdminPolicyResponse(string Category, string SettingsJson, int Version, DateTimeOffset? UpdatedAt);
