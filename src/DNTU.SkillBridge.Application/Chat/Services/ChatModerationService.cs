using DNTU.SkillBridge.Application.Chat.Repositories;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Chat;
using System.Text.Json;

namespace DNTU.SkillBridge.Application.Chat.Services;

/// <summary>Admin xử lý báo cáo vi phạm chat, cảnh cáo hoặc khóa quyền chat tài khoản spam.</summary>
public sealed class ChatModerationService(
    IChatRepository chatRepository,
    INotificationWriter? notificationWriter = null) : IChatModerationService
{
    public async Task<PagedResponse<ChatReportAdminResponse>> GetReportsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var normalizedStatus = NormalizeStatus(status);
        var rows = await chatRepository.ListReportsAsync(normalizedStatus, (page - 1) * pageSize, pageSize, cancellationToken);
        var total = await chatRepository.CountReportsAsync(normalizedStatus, cancellationToken);
        var messageIds = rows.Where(r => r.Message is not null).Select(r => r.MessageId).ToList();
        var reactions = await chatRepository.GetReactionsForMessagesAsync(messageIds, cancellationToken);
        var reactionMap = reactions
            .GroupBy(r => r.MessageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ChatMessageReactionSummary>)g
                .Select(r => new ChatMessageReactionSummary(r.Emoji, r.Count, r.UserIds))
                .ToList());

        var items = rows.Select(row => new ChatReportAdminResponse(
            row.Id,
            row.MessageId,
            row.Message is null ? null : MapMessage(row.Message, reactionMap.TryGetValue(row.MessageId, out var reactions_) ? reactions_ : []),
            row.ReporterUserId,
            row.ReporterDisplayName,
            row.Reason,
            row.Status,
            row.ResolvedByAdminUserId,
            row.ResolutionNote,
            row.ResolvedAt,
            row.CreatedAt)).ToList();

        return new PagedResponse<ChatReportAdminResponse>(items, PageMetadata.Create(page, pageSize, total));
    }

    public async Task<(ChatOutcome, string?)> ResolveReportAsync(Guid adminUserId, Guid reportId, ResolveChatReportRequest request, CancellationToken cancellationToken)
    {
        var report = await chatRepository.GetReportAsync(reportId, cancellationToken);
        if (report is null) return (ChatOutcome.MessageNotFound, "Không tìm thấy báo cáo.");

        var decision = ParseDecision(request.Decision);
        if (decision is null) return (ChatOutcome.InvalidOperation, "Quyết định xử lý không hợp lệ.");

        if (!report.Resolve(adminUserId, decision.Value, request.Note))
        {
            return (ChatOutcome.InvalidOperation, "Báo cáo này đã được xử lý trước đó.");
        }

        // Áp dụng hình thức xử lý với người gửi tin nhắn bị báo cáo.
        var message = await chatRepository.GetMessageAsync(report.MessageId, cancellationToken);
        if (message is not null && message.SenderUserId != Guid.Empty)
        {
            var senderName = (await chatRepository.GetUserDisplayAsync(message.SenderUserId, cancellationToken))?.DisplayName ?? "Người dùng";
            switch (decision.Value)
            {
                case ChatReportDecision.WARNED:
                    notificationWriter?.Add(new NotificationMessage(
                        message.SenderUserId,
                        "CHAT_WARNED",
                        "Cảnh cáo: tin nhắn của bạn vi phạm quy tắc cộng đồng",
                        JsonSerializer.Serialize(new { reportId, reason = request.Note })));
                    break;
                case ChatReportDecision.BANNED:
                    var until = request.BanDays is > 0 ? (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(request.BanDays.Value) : null;
                    await chatRepository.BanChatAsync(message.SenderUserId, request.Note ?? "Vi phạm quy tắc nhắn tin", until, cancellationToken);
                    notificationWriter?.Add(new NotificationMessage(
                        message.SenderUserId,
                        "CHAT_BANNED",
                        until.HasValue ? $"Quyền nhắn tin của {senderName} bị khóa {request.BanDays.Value} ngày" : "Quyền nhắn tin của bạn đã bị khóa",
                        JsonSerializer.Serialize(new { reportId, until })));
                    break;
            }
        }

        await chatRepository.SaveAsync(cancellationToken);

        // Thông báo cho người báo cáo biết kết quả.
        notificationWriter?.Add(new NotificationMessage(
            report.ReporterUserId,
            "CHAT_REPORT_RESOLVED",
            "Báo cáo của bạn đã được xử lý",
            JsonSerializer.Serialize(new { reportId })));
        await chatRepository.SaveAsync(cancellationToken);
        return (ChatOutcome.Success, null);
    }

    public async Task<(ChatOutcome, string?)> SetChatBanAsync(Guid targetUserId, string? reason, int? banDays, CancellationToken cancellationToken)
    {
        var user = await chatRepository.GetUserChatStateAsync(targetUserId, cancellationToken);
        if (user is null) return (ChatOutcome.TargetNotFound, "Không tìm thấy người dùng.");

        var until = banDays is > 0 ? (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(banDays.Value) : null;
        await chatRepository.BanChatAsync(targetUserId, string.IsNullOrWhiteSpace(reason) ? "Vi phạm quy tắc nhắn tin" : reason.Trim(), until, cancellationToken);
        await chatRepository.SaveAsync(cancellationToken);

        notificationWriter?.Add(new NotificationMessage(
            targetUserId,
            "CHAT_BANNED",
            until.HasValue ? $"Quyền nhắn tin của bạn bị khóa {banDays.Value} ngày" : "Quyền nhắn tin của bạn đã bị khóa",
            JsonSerializer.Serialize(new { until })));
        await chatRepository.SaveAsync(cancellationToken);
        return (ChatOutcome.Success, null);
    }

    public async Task<(ChatOutcome, string?)> RemoveChatBanAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        var user = await chatRepository.GetUserChatStateAsync(targetUserId, cancellationToken);
        if (user is null) return (ChatOutcome.TargetNotFound, "Không tìm thấy người dùng.");

        await chatRepository.UnbanChatAsync(targetUserId, cancellationToken);
        await chatRepository.SaveAsync(cancellationToken);
        return (ChatOutcome.Success, null);
    }

    public async Task<ChatModerationStatsResponse> GetModerationStatsAsync(CancellationToken cancellationToken) =>
        new(
            await chatRepository.CountReportsAsync("OPEN", cancellationToken),
            await chatRepository.CountReportsAsync(null, cancellationToken),
            await chatRepository.CountAllConversationsAsync(cancellationToken),
            await chatRepository.CountAllMessagesAsync(cancellationToken),
            await chatRepository.CountChatBannedUsersAsync(cancellationToken));

    private static string? NormalizeStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "OPEN" => "OPEN",
        "RESOLVED" => "RESOLVED",
        "DISMISSED" => "DISMISSED",
        _ => null,
    };

    private static ChatReportDecision? ParseDecision(string? decision) => decision?.ToUpperInvariant() switch
    {
        "DISMISSED" => ChatReportDecision.DISMISSED,
        "WARNED" => ChatReportDecision.WARNED,
        "BANNED" => ChatReportDecision.BANNED,
        _ => null,
    };

    private static ChatMessageResponse MapMessage(ChatMessageRow row, IReadOnlyCollection<ChatMessageReactionSummary> reactions) =>
        new(
            row.Id,
            row.ConversationId,
            row.SenderUserId,
            row.SenderDisplayName,
            row.SenderAvatarUrl,
            row.MessageType.ToString(),
            row.Content,
            row.MetadataJson,
            row.ReplyToMessageId,
            row.ReplyToPreview,
            row.ReplyToSenderName,
            row.IsEdited,
            row.EditedAt,
            row.IsDeleted,
            reactions,
            row.CreatedAt);
}
