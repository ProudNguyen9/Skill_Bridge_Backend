using DNTU.SkillBridge.Application.Common;

namespace DNTU.SkillBridge.Application.Chat;

/// <summary>Hàng đợi kiểm duyệt chat của quản trị viên.</summary>
public interface IChatModerationService
{
    Task<PagedResponse<ChatReportAdminResponse>> GetReportsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, string? ErrorMessage)> ResolveReportAsync(Guid adminUserId, Guid reportId, ResolveChatReportRequest request, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, string? ErrorMessage)> SetChatBanAsync(Guid targetUserId, string? reason, int? banDays, CancellationToken cancellationToken);

    Task<(ChatOutcome Outcome, string? ErrorMessage)> RemoveChatBanAsync(Guid targetUserId, CancellationToken cancellationToken);

    Task<ChatModerationStatsResponse> GetModerationStatsAsync(CancellationToken cancellationToken);
}
