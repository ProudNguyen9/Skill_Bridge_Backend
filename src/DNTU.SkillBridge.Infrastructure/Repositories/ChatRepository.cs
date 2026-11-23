using System.Linq.Expressions;
using DNTU.SkillBridge.Application.Chat;
using DNTU.SkillBridge.Application.Chat.Repositories;
using DNTU.SkillBridge.Domain.Chat;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Social;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation của contract chat. Read dùng projection + AsNoTracking.</summary>
public sealed class ChatRepository(AppDbContext dbContext) : IChatRepository
{
    private static readonly ChatMessageType[] CountableMessageTypes = [ChatMessageType.TEXT, ChatMessageType.IMAGE, ChatMessageType.FILE, ChatMessageType.CONTEXT_CARD];

    // ---------- Hội thoại ----------

    public Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken) =>
        dbContext.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

    public async Task<Conversation?> GetDirectConversationAsync(Guid userA, Guid userB, CancellationToken cancellationToken)
    {
        var pairKey = Domain.Chat.Conversation.BuildDirectPairKey(userA, userB);
        return await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DirectPairKey == pairKey, cancellationToken);
    }

    public Task<Conversation?> GetProjectConversationAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Type == ConversationType.PROJECT_GROUP, cancellationToken);

    public Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.ConversationParticipants
            .AsNoTracking()
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId && p.LeftAt == null, cancellationToken);

    public Task<bool> IsAcceptedParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.ConversationParticipants
            .AsNoTracking()
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId && p.LeftAt == null && p.RequestStatus == ConversationRequestStatus.ACCEPTED, cancellationToken);

    public async Task<IReadOnlyCollection<(Guid UserId, ConversationRequestStatus Status, DateTimeOffset? LeftAt)>> GetParticipantStatusesAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.ConversationId == conversationId)
            .Select(p => new { p.UserId, p.RequestStatus, p.LeftAt })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.UserId, r.RequestStatus, r.LeftAt)).ToList();
    }

    public async Task<IReadOnlyList<ChatParticipantReadStateRow>> GetParticipantReadStatesAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.ConversationId == conversationId)
            .Select(p => new ChatParticipantReadStateRow(p.UserId, p.LastReadMessageId, p.LastReadAt))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<int> GetUnreadCountAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var lastReadAt = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.ConversationId == conversationId && p.UserId == userId)
            .Select(p => p.LastReadAt)
            .FirstOrDefaultAsync(cancellationToken);

        return await dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId
                && !m.IsDeleted
                && CountableMessageTypes.Contains(m.MessageType)
                && m.SenderUserId != userId
                && (lastReadAt == null || m.CreatedAt > lastReadAt))
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsForConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var participants = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.ConversationId == conversationId && p.LeftAt == null)
            .Select(p => new { p.UserId, p.LastReadAt })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>(participants.Count);
        foreach (var participant in participants)
        {
            result[participant.UserId] = await dbContext.ChatMessages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId
                    && !m.IsDeleted
                    && CountableMessageTypes.Contains(m.MessageType)
                    && m.SenderUserId != participant.UserId
                    && (participant.LastReadAt == null || m.CreatedAt > participant.LastReadAt))
                .CountAsync(cancellationToken);
        }

        return result;
    }

    public async Task<ChatConversationRow?> GetConversationRowAsync(Guid conversationId, Guid viewerUserId, CancellationToken cancellationToken)
    {
        var viewerRow = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId && p.UserId == viewerUserId)
            .Select(p => new { p.IsMuted, p.IsPinned, p.RequestStatus, p.LastReadAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (viewerRow is null) return null;

        var conversation = await dbContext.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new
            {
                c.Id,
                c.Type,
                c.ProjectId,
                ProjectTitle = c.Project != null ? c.Project.Title : null,
                c.Title,
                c.AvatarUrl,
                c.CreatedAt,
                c.LastMessageAt,
                LastMessageId = c.LastMessageId,
                LastMessagePreview = c.LastMessagePreview,
                LastMessageSenderUserId = c.LastMessage != null ? (Guid?)c.LastMessage.SenderUserId : null,
                LastMessageSenderName = c.LastMessage != null && c.LastMessage.SenderUserId != Guid.Empty ? c.LastMessage.SenderUser.DisplayName : null,
                LastMessageType = c.LastMessage != null ? (ChatMessageType?)c.LastMessage.MessageType : null,
                LastMessageCreatedAt = c.LastMessage != null ? (DateTimeOffset?)c.LastMessage.CreatedAt : null,
                ParticipantsCount = c.Participants.Count(p => p.LeftAt == null),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (conversation is null) return null;

        var unreadCount = await GetUnreadCountAsync(conversationId, viewerUserId, cancellationToken);
        Guid? otherUserId = null;
        if (conversation.Type == ConversationType.DIRECT)
        {
            otherUserId = await dbContext.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && p.UserId != viewerUserId && p.LeftAt == null)
                .Select(p => (Guid?)p.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new ChatConversationRow(
            conversation.Id,
            conversation.Type,
            conversation.ProjectId,
            conversation.ProjectTitle,
            conversation.Title,
            conversation.AvatarUrl,
            conversation.LastMessageId is null ? null : new ChatMessageSummary(
                conversation.LastMessageId.Value,
                conversation.LastMessagePreview ?? string.Empty,
                conversation.LastMessageSenderUserId ?? Guid.Empty,
                conversation.LastMessageSenderName ?? string.Empty,
                (conversation.LastMessageType ?? ChatMessageType.TEXT).ToString(),
                conversation.LastMessageCreatedAt ?? conversation.CreatedAt),
            unreadCount,
            viewerRow.IsMuted,
            viewerRow.IsPinned,
            viewerRow.RequestStatus,
            conversation.ParticipantsCount,
            conversation.CreatedAt,
            conversation.LastMessageAt,
            otherUserId,
            otherUserId.HasValue ? await GetUserDisplayNameAsync(otherUserId.Value, cancellationToken) : null,
            otherUserId.HasValue ? await ResolveProfileAvatarAsync(otherUserId.Value, cancellationToken) : null,
            otherUserId.HasValue ? await ResolveProfileRoleAsync(otherUserId.Value, cancellationToken) : null);
    }

    public async Task<IReadOnlyList<ChatParticipantRow>> GetConversationParticipantsAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId)
            .Select(p => new { p.UserId, ParticipantRole = p.Role, p.RequestStatus, p.LastReadMessageId, p.LastReadAt, p.LeftAt })
            .ToListAsync(cancellationToken);

        var result = new List<ChatParticipantRow>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new ChatParticipantRow(
                row.UserId,
                await GetUserDisplayNameAsync(row.UserId, cancellationToken) ?? string.Empty,
                await ResolveProfileAvatarAsync(row.UserId, cancellationToken),
                await ResolveProfileRoleAsync(row.UserId, cancellationToken),
                row.ParticipantRole,
                row.RequestStatus,
                row.LastReadMessageId,
                row.LastReadAt,
                row.LeftAt));
        }

        return result;
    }

    public async Task<(Guid UserId, string DisplayName, string? AvatarUrl, string Role)?> GetUserDisplayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;
        return (user.Id, user.DisplayName, await ResolveProfileAvatarAsync(userId, cancellationToken), await ResolveProfileRoleAsync(userId, cancellationToken));
    }

    // ---------- Hộp thư ----------

    public async Task<IReadOnlyList<ChatConversationRow>> ListConversationsAsync(Guid userId, ConversationInboxTab tab, string? search, int skip, int take, CancellationToken cancellationToken)
    {
        var query = BuildInboxQuery(userId, tab, search);
        var rows = await query
            .OrderByDescending(r => r.IsPinned)
            .ThenByDescending(r => r.LastMessageAt ?? r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return [];

        var conversationIds = rows.Select(r => r.Id).ToList();

        // Batch thông tin "đối phương" cho hội thoại Direct của cả trang.
        var others = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => conversationIds.Contains(p.ConversationId) && p.UserId != userId && p.LeftAt == null)
            .Select(p => new { p.ConversationId, p.UserId, p.User.DisplayName })
            .ToListAsync(cancellationToken);
        var otherByConversation = others
            .GroupBy(o => o.ConversationId)
            .ToDictionary(g => g.Key, g => g.First());

        // Batch số chưa đọc của cả trang: gom LastReadAt của viewer.
        var readStates = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => conversationIds.Contains(p.ConversationId) && p.UserId == userId)
            .Select(p => new { p.ConversationId, p.LastReadAt })
            .ToListAsync(cancellationToken);
        var lastReadByConversation = readStates.ToDictionary(r => r.ConversationId, r => r.LastReadAt);

        var unreadMessages = await dbContext.ChatMessages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId)
                && !m.IsDeleted
                && CountableMessageTypes.Contains(m.MessageType)
                && m.SenderUserId != userId)
            .Select(m => new { m.ConversationId, m.CreatedAt })
            .ToListAsync(cancellationToken);

        var result = new List<ChatConversationRow>(rows.Count);
        foreach (var row in rows)
        {
            lastReadByConversation.TryGetValue(row.Id, out var lastReadAt);
            var unread = unreadMessages.Count(m => m.ConversationId == row.Id && (lastReadAt == null || m.CreatedAt > lastReadAt));

            Guid? otherUserId = null;
            string? otherName = null;
            if (row.Type == ConversationType.DIRECT && otherByConversation.TryGetValue(row.Id, out var other))
            {
                otherUserId = other.UserId;
                otherName = other.DisplayName;
            }

            result.Add(new ChatConversationRow(
                row.Id,
                row.Type,
                row.ProjectId,
                row.ProjectTitle,
                row.Title,
                row.AvatarUrl,
                row.LastMessageId is null ? null : new ChatMessageSummary(
                    row.LastMessageId.Value,
                    row.LastMessagePreview ?? string.Empty,
                    row.LastMessageSenderUserId,
                    row.LastMessageSenderName ?? string.Empty,
                    row.LastMessageType.ToString(),
                    row.LastMessageCreatedAt ?? row.CreatedAt),
                unread,
                row.IsMuted,
                row.IsPinned,
                row.RequestStatus,
                row.ParticipantsCount,
                row.CreatedAt,
                row.LastMessageAt,
                otherUserId,
                otherName,
                otherUserId.HasValue ? await ResolveProfileAvatarAsync(otherUserId.Value, cancellationToken) : null,
                otherUserId.HasValue ? await ResolveProfileRoleAsync(otherUserId.Value, cancellationToken) : null));
        }

        return result;
    }

    public Task<int> CountConversationsAsync(Guid userId, ConversationInboxTab tab, string? search, CancellationToken cancellationToken) =>
        BuildInboxQuery(userId, tab, search).CountAsync(cancellationToken);

    private IQueryable<InboxRow> BuildInboxQuery(Guid userId, ConversationInboxTab tab, string? search)
    {
        var query = dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => p.UserId == userId && p.LeftAt == null);

        query = tab switch
        {
            ConversationInboxTab.UNREAD => query.Where(p =>
                p.RequestStatus == ConversationRequestStatus.ACCEPTED
                && dbContext.ChatMessages.Any(m => m.ConversationId == p.ConversationId
                    && !m.IsDeleted
                    && CountableMessageTypes.Contains(m.MessageType)
                    && m.SenderUserId != userId
                    && (p.LastReadAt == null || m.CreatedAt > p.LastReadAt))),
            ConversationInboxTab.GROUPS => query.Where(p =>
                p.Conversation.Type == ConversationType.PROJECT_GROUP
                && p.RequestStatus != ConversationRequestStatus.DECLINED),
            ConversationInboxTab.REQUESTS => query.Where(p => p.RequestStatus == ConversationRequestStatus.PENDING_REQUEST),
            // Hộp thư chính chỉ gồm hội thoại đã chấp nhận (tin nhắn chờ nằm ở tab REQUESTS).
            _ => query.Where(p => p.RequestStatus == ConversationRequestStatus.ACCEPTED),
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                (p.Conversation.Title != null && p.Conversation.Title.Contains(term)) ||
                (p.Conversation.Project != null && p.Conversation.Project.Title.Contains(term)) ||
                p.Conversation.Participants.Any(other => other.UserId != userId && other.User.DisplayName.Contains(term)));
        }

        return query.Select(p => new InboxRow
        {
            Id = p.Conversation.Id,
            Type = p.Conversation.Type,
            ProjectId = p.Conversation.ProjectId,
            ProjectTitle = p.Conversation.Project != null ? p.Conversation.Project.Title : null,
            Title = p.Conversation.Title,
            AvatarUrl = p.Conversation.AvatarUrl,
            CreatedAt = p.Conversation.CreatedAt,
            LastMessageAt = p.Conversation.LastMessageAt,
            LastMessageId = p.Conversation.LastMessageId,
            LastMessagePreview = p.Conversation.LastMessagePreview,
            LastMessageSenderUserId = p.Conversation.LastMessage != null ? p.Conversation.LastMessage.SenderUserId : Guid.Empty,
            LastMessageSenderName = p.Conversation.LastMessage != null && p.Conversation.LastMessage.SenderUserId != Guid.Empty ? p.Conversation.LastMessage.SenderUser.DisplayName : null,
            LastMessageType = p.Conversation.LastMessage != null ? p.Conversation.LastMessage.MessageType : ChatMessageType.TEXT,
            LastMessageCreatedAt = p.Conversation.LastMessage != null ? p.Conversation.LastMessage.CreatedAt : null,
            ParticipantsCount = p.Conversation.Participants.Count(other => other.LeftAt == null),
            IsMuted = p.IsMuted,
            IsPinned = p.IsPinned,
            RequestStatus = p.RequestStatus,
        });
    }

    public async Task<int> GetTotalUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => p.UserId == userId && p.LeftAt == null && p.RequestStatus != ConversationRequestStatus.DECLINED)
            .Select(p => new { p.ConversationId, p.LastReadAt })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return 0;

        var conversationIds = rows.Select(r => r.ConversationId).ToList();
        var messages = await dbContext.ChatMessages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId)
                && !m.IsDeleted
                && CountableMessageTypes.Contains(m.MessageType)
                && m.SenderUserId != userId)
            .Select(m => new { m.ConversationId, m.CreatedAt })
            .ToListAsync(cancellationToken);

        var lastReadByConversation = rows.ToDictionary(r => r.ConversationId, r => r.LastReadAt);
        var total = 0;
        foreach (var group in messages.GroupBy(m => m.ConversationId))
        {
            lastReadByConversation.TryGetValue(group.Key, out var lastReadAt);
            total += group.Count(m => lastReadAt == null || m.CreatedAt > lastReadAt);
        }

        return total;
    }

    // ---------- Tin nhắn ----------

    public Task<ChatMessage?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken) =>
        dbContext.ChatMessages
            .Include(m => m.Reactions)
            .Include(m => m.Conversation)
            .ThenInclude(c => c.Participants)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

    public Task<ChatMessageRow?> GetMessageRowAsync(Guid messageId, CancellationToken cancellationToken) =>
        dbContext.ChatMessages.AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(MessageRowProjection)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatMessageRow>> ListMessagesAsync(Guid conversationId, DateTimeOffset? beforeCreatedAt, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId);
        if (beforeCreatedAt.HasValue)
        {
            query = query.Where(m => m.CreatedAt < beforeCreatedAt.Value);
        }

        var rows = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(MessageRowProjection)
            .ToListAsync(cancellationToken);

        return rows.AsEnumerable().Reverse().ToList();
    }

    /// <summary>Projection tin nhắn kèm avatar người gửi (correlated subquery qua 3 bảng profile).</summary>
    private Expression<Func<ChatMessage, ChatMessageRow>> MessageRowProjection =>
        m => new ChatMessageRow(
            m.Id,
            m.ConversationId,
            m.SenderUserId,
            m.SenderUserId == Guid.Empty ? "Hệ thống" : m.SenderUser.DisplayName,
            m.SenderUserId == Guid.Empty
                ? null
                : (dbContext.StudentProfiles.Where(s => s.UserId == m.SenderUserId).Select(s => s.AvatarUrl).FirstOrDefault()
                    ?? dbContext.LecturerProfiles.Where(l => l.UserId == m.SenderUserId).Select(l => l.AvatarUrl).FirstOrDefault()
                    ?? dbContext.CompanyMembers.Where(c => c.UserId == m.SenderUserId).Select(c => c.Company.LogoUrl).FirstOrDefault()),
            m.MessageType,
            m.Content,
            m.MetadataJson,
            m.ReplyToMessageId,
            m.ReplyTo != null ? m.ReplyTo.Content : null,
            m.ReplyTo != null && m.ReplyTo.SenderUserId != Guid.Empty ? m.ReplyTo.SenderUser.DisplayName : "Hệ thống",
            m.IsEdited,
            m.EditedAt,
            m.IsDeleted,
            m.CreatedAt);

    public async Task<IReadOnlyList<ChatMessageReactionRow>> GetReactionsForMessagesAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0) return [];
        var rows = await dbContext.ChatMessageReactions.AsNoTracking()
            .Where(r => messageIds.Contains(r.MessageId))
            .Select(r => new { r.MessageId, r.Emoji, r.UserId })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => new { r.MessageId, r.Emoji })
            .Select(g => new ChatMessageReactionRow(g.Key.MessageId, g.Key.Emoji, g.Count(), g.Select(x => x.UserId).ToList()))
            .ToList();
    }

    // ---------- Quan hệ / chống spam ----------

    public async Task<bool> HasDirectRelationshipAsync(Guid userA, Guid userB, CancellationToken cancellationToken)
    {
        // 1. Chung dự án: cả hai đều có quyền truy cập cùng một dự án (SV-SV, DN-DN, DN-SV, GV-...).
        if (await SharedProjectExistsAsync(userA, userB, cancellationToken)) return true;

        // 2. Đơn ứng tuyển liên quan DN ↔ SV.
        var companyUserA = await CompanyUserOfAsync(userA, cancellationToken);
        var companyUserB = await CompanyUserOfAsync(userB, cancellationToken);
        if (companyUserA.HasValue && !companyUserB.HasValue)
        {
            if (await dbContext.Applications.AsNoTracking()
                .AnyAsync(a => a.Student.UserId == userB && a.Project.Company.Members.Any(m => m.UserId == userA), cancellationToken)) return true;
        }
        else if (companyUserB.HasValue && !companyUserA.HasValue)
        {
            if (await dbContext.Applications.AsNoTracking()
                .AnyAsync(a => a.Student.UserId == userA && a.Project.Company.Members.Any(m => m.UserId == userB), cancellationToken)) return true;
        }

        // 3. Lời mời GVHD liên quan (LecturerAssignments.LecturerId tham chiếu LecturerProfiles.Id).
        var lecturerProfileA = await LecturerProfileIdOfAsync(userA, cancellationToken);
        var lecturerProfileB = await LecturerProfileIdOfAsync(userB, cancellationToken);
        if (lecturerProfileA.HasValue && !lecturerProfileB.HasValue)
        {
            if (await dbContext.LecturerAssignments.AsNoTracking()
                .AnyAsync(a => a.LecturerId == lecturerProfileA.Value && a.InvitedByUserId == userB, cancellationToken)) return true;
        }
        else if (lecturerProfileB.HasValue && !lecturerProfileA.HasValue)
        {
            if (await dbContext.LecturerAssignments.AsNoTracking()
                .AnyAsync(a => a.LecturerId == lecturerProfileB.Value && a.InvitedByUserId == userA, cancellationToken)) return true;
        }

        // 4. Đã theo dõi lẫn nhau.
        var followsAtoB = await dbContext.UserFollows.AsNoTracking()
            .AnyAsync(f => f.FollowerUserId == userA && f.FolloweeUserId == userB, cancellationToken);
        var followsBtoA = await dbContext.UserFollows.AsNoTracking()
            .AnyAsync(f => f.FollowerUserId == userB && f.FolloweeUserId == userA, cancellationToken);
        return followsAtoB && followsBtoA;
    }

    private async Task<bool> SharedProjectExistsAsync(Guid userA, Guid userB, CancellationToken cancellationToken)
    {
        var aStudentProjects = dbContext.ProjectMembers.AsNoTracking()
            .Where(m => m.IsActive && m.Student.UserId == userA)
            .Select(m => m.ProjectId);
        var bStudentProjects = dbContext.ProjectMembers.AsNoTracking()
            .Where(m => m.IsActive && m.Student.UserId == userB)
            .Select(m => m.ProjectId);
        if (await aStudentProjects.AnyAsync(p => bStudentProjects.Contains(p), cancellationToken)) return true;

        var aCompanyProjects = dbContext.Projects.AsNoTracking()
            .Where(p => p.Company.Members.Any(m => m.UserId == userA))
            .Select(p => p.Id);
        var bCompanyProjects = dbContext.Projects.AsNoTracking()
            .Where(p => p.Company.Members.Any(m => m.UserId == userB))
            .Select(p => p.Id);
        if (await aCompanyProjects.AnyAsync(p => bCompanyProjects.Contains(p), cancellationToken)) return true;
        if (await aCompanyProjects.AnyAsync(p => bStudentProjects.Contains(p), cancellationToken)) return true;
        if (await bCompanyProjects.AnyAsync(p => aStudentProjects.Contains(p), cancellationToken)) return true;

        var aLecturerProjects = dbContext.LecturerAssignments.AsNoTracking()
            .Where(a => a.Status == LecturerAssignmentStatus.ACTIVE)
            .Join(dbContext.LecturerProfiles.AsNoTracking(),
                a => a.LecturerId,
                l => l.Id,
                (a, l) => new { a.ProjectId, l.UserId })
            .Where(x => x.UserId == userA)
            .Select(x => x.ProjectId);
        var bLecturerProjects = dbContext.LecturerAssignments.AsNoTracking()
            .Where(a => a.Status == LecturerAssignmentStatus.ACTIVE)
            .Join(dbContext.LecturerProfiles.AsNoTracking(),
                a => a.LecturerId,
                l => l.Id,
                (a, l) => new { a.ProjectId, l.UserId })
            .Where(x => x.UserId == userB)
            .Select(x => x.ProjectId);
        if (await aLecturerProjects.AnyAsync(p => bStudentProjects.Contains(p), cancellationToken)) return true;
        if (await bLecturerProjects.AnyAsync(p => aStudentProjects.Contains(p), cancellationToken)) return true;
        if (await aLecturerProjects.AnyAsync(p => bCompanyProjects.Contains(p), cancellationToken)) return true;
        if (await bLecturerProjects.AnyAsync(p => aCompanyProjects.Contains(p), cancellationToken)) return true;
        if (await aLecturerProjects.AnyAsync(p => bLecturerProjects.Contains(p), cancellationToken)) return true;

        return false;
    }

    private Task<Guid?> CompanyUserOfAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.CompanyMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => (Guid?)m.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<Guid?> LecturerProfileIdOfAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken) =>
        dbContext.UserBlocks.AsNoTracking()
            .AnyAsync(b => (b.BlockerUserId == userA && b.BlockedUserId == userB) || (b.BlockerUserId == userB && b.BlockedUserId == userA), cancellationToken);

    public Task<int> CountNewPendingRequestsCreatedTodayAsync(Guid userId, DateTimeOffset dayStartUtc, CancellationToken cancellationToken) =>
        dbContext.Conversations.AsNoTracking()
            .Where(c => c.Type == ConversationType.DIRECT
                && c.CreatedByUserId == userId
                && c.CreatedAt >= dayStartUtc
                && c.Participants.Any(p => p.UserId != userId && p.RequestStatus == ConversationRequestStatus.PENDING_REQUEST))
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatDirectoryRow>> SearchDirectoryAsync(Guid userId, string? query, int limit, CancellationToken cancellationToken)
    {
        IQueryable<User> usersQuery = dbContext.Users.AsNoTracking()
            .Where(u => u.Id != userId
                && u.IsActive
                && u.UserRoles.Any(r => r.Role.Name != RoleNames.Admin && r.Role.Name != RoleNames.SuperAdmin));
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            usersQuery = usersQuery.Where(u => u.DisplayName.Contains(term) || u.Email.Contains(term));
        }

        var users = await usersQuery
            .OrderBy(u => u.DisplayName)
            .Take(limit)
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(cancellationToken);
        if (users.Count == 0) return [];

        var userIds = users.Select(u => u.Id).ToList();

        var studentInfos = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .Select(s => new { s.UserId, s.StudentCode, s.AvatarUrl })
            .ToListAsync(cancellationToken);
        var studentMap = studentInfos.ToDictionary(s => s.UserId);

        var lecturerInfos = await dbContext.LecturerProfiles.AsNoTracking()
            .Where(l => userIds.Contains(l.UserId))
            .Select(l => new { l.UserId, l.AcademicTitle, l.Department, l.AvatarUrl })
            .ToListAsync(cancellationToken);
        var lecturerMap = lecturerInfos.ToDictionary(l => l.UserId);

        var companyInfos = await dbContext.CompanyMembers.AsNoTracking()
            .Where(m => userIds.Contains(m.UserId))
            .Select(m => new { m.UserId, CompanyName = m.Company.Name, LogoUrl = m.Company.LogoUrl })
            .ToListAsync(cancellationToken);
        var companyMap = companyInfos.GroupBy(c => c.UserId).ToDictionary(g => g.Key, g => g.First());

        var existingIds = new Dictionary<Guid, Guid>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var keys = users.Select(u => Domain.Chat.Conversation.BuildDirectPairKey(userId, u.Id)).ToList();
            var existing = await dbContext.Conversations.AsNoTracking()
                .Where(c => c.DirectPairKey != null && keys.Contains(c.DirectPairKey))
                .Select(c => new { c.Id, c.DirectPairKey })
                .ToListAsync(cancellationToken);
            foreach (var item in existing)
            {
                var parts = item.DirectPairKey!.Split('_', 2);
                if (parts.Length == 2
                    && Guid.TryParseExact(parts[0], "N", out var first)
                    && Guid.TryParseExact(parts[1], "N", out var second))
                {
                    var otherId = first == userId ? second : first;
                    existingIds[otherId] = item.Id;
                }
            }
        }

        var results = new List<ChatDirectoryRow>(users.Count);
        foreach (var user in users)
        {
            string role;
            string? detail = null;
            string? avatarUrl = null;
            if (studentMap.TryGetValue(user.Id, out var student))
            {
                role = RoleNames.Student;
                detail = student.StudentCode;
                avatarUrl = student.AvatarUrl;
            }
            else if (lecturerMap.TryGetValue(user.Id, out var lecturer))
            {
                role = RoleNames.Lecturer;
                detail = string.Join(" - ", new[] { lecturer.AcademicTitle, lecturer.Department }.Where(x => !string.IsNullOrWhiteSpace(x)));
                avatarUrl = lecturer.AvatarUrl;
            }
            else if (companyMap.TryGetValue(user.Id, out var company))
            {
                role = RoleNames.Company;
                detail = company.CompanyName;
                avatarUrl = company.LogoUrl;
            }
            else
            {
                role = RoleNames.Student;
            }

            Guid? existingConversationId = existingIds.TryGetValue(user.Id, out var foundConversationId) ? foundConversationId : null;
            results.Add(new ChatDirectoryRow(user.Id, user.DisplayName, avatarUrl, role, detail, existingConversationId));
        }

        return results;
    }

    public Task<(Guid UserId, string DisplayName, string? ChatBanReason, DateTimeOffset? ChatBannedUntil)?> GetUserChatStateAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.DisplayName, u.ChatBanReason, u.ChatBannedUntil })
            .FirstOrDefaultAsync(cancellationToken)
            .ContinueWith(t => t.Result == null
                ? ((Guid UserId, string DisplayName, string? ChatBanReason, DateTimeOffset? ChatBannedUntil)?)null
                : (t.Result.Id, t.Result.DisplayName, t.Result.ChatBanReason, t.Result.ChatBannedUntil), cancellationToken);

    public Task<bool> IsAdminUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .AnyAsync(u => u.UserRoles.Any(r => r.Role.Name == RoleNames.Admin || r.Role.Name == RoleNames.SuperAdmin), cancellationToken);

    public Task<bool> HasOpenReportAsync(Guid messageId, Guid reporterUserId, CancellationToken cancellationToken) =>
        dbContext.ChatReports.AsNoTracking()
            .AnyAsync(r => r.MessageId == messageId && r.ReporterUserId == reporterUserId && r.Status == ChatReportStatus.OPEN, cancellationToken);

    public async Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var studentAccess = await dbContext.ProjectMembers.AsNoTracking()
            .AnyAsync(m => m.ProjectId == projectId && m.IsActive && m.Student.UserId == userId, cancellationToken);
        if (studentAccess) return true;

        var companyAccess = await dbContext.Projects.AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.Company.Members.Any(m => m.UserId == userId), cancellationToken);
        if (companyAccess) return true;

        var lecturerAccess = await dbContext.LecturerAssignments.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.Status == LecturerAssignmentStatus.ACTIVE)
            .Join(dbContext.LecturerProfiles.AsNoTracking(),
                a => a.LecturerId,
                l => l.Id,
                (a, l) => l.UserId)
            .AnyAsync(luid => luid == userId, cancellationToken);
        return lecturerAccess;
    }

    public async Task<Guid?> GetCompanyPrimaryUserAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var ownerId = await dbContext.CompanyMembers.AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.Role == CompanyMemberRole.OWNER)
            .Select(m => (Guid?)m.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (ownerId.HasValue) return ownerId;

        return await dbContext.CompanyMembers.AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .OrderBy(m => m.Role)
            .Select(m => (Guid?)m.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ---------- Đồng bộ nhóm dự án ----------

    public Task<(Guid ProjectId, string Title)?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Title })
            .FirstOrDefaultAsync(cancellationToken)
            .ContinueWith(t => t.Result == null ? ((Guid, string)?)null : (t.Result.Id, t.Result.Title), cancellationToken);

    public async Task<ProjectConversationMembers?> GetProjectConversationMembersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId, cancellationToken);
        if (!exists) return null;

        var companyUsers = await dbContext.CompanyMembers.AsNoTracking()
            .Join(dbContext.Projects.AsNoTracking().Where(p => p.Id == projectId),
                m => m.CompanyId,
                p => p.CompanyId,
                (m, p) => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var lecturerUsers = await dbContext.LecturerAssignments.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.Status == LecturerAssignmentStatus.ACTIVE)
            .Join(dbContext.LecturerProfiles.AsNoTracking(),
                a => a.LecturerId,
                l => l.Id,
                (a, l) => l.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var studentUsers = await dbContext.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.IsActive)
            .Join(dbContext.StudentProfiles.AsNoTracking(),
                m => m.StudentId,
                s => s.Id,
                (m, s) => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new ProjectConversationMembers(companyUsers, lecturerUsers, studentUsers);
    }

    // ---------- Ghi ----------

    public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        dbContext.Conversations.Add(conversation);
        return Task.CompletedTask;
    }

    public Task AddParticipantAsync(ConversationParticipant participant, CancellationToken cancellationToken)
    {
        dbContext.ConversationParticipants.Add(participant);
        return Task.CompletedTask;
    }

    public Task AddReactionAsync(ChatMessageReaction reaction, CancellationToken cancellationToken)
    {
        dbContext.ChatMessageReactions.Add(reaction);
        return Task.CompletedTask;
    }

    public Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        dbContext.ChatMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task AddReportAsync(ChatReport report, CancellationToken cancellationToken)
    {
        dbContext.ChatReports.Add(report);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    // ---------- Kiểm duyệt ----------

    public Task<ChatReport?> GetReportAsync(Guid reportId, CancellationToken cancellationToken) =>
        dbContext.ChatReports.FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

    public async Task<IReadOnlyList<ChatReportAdminRow>> ListReportsAsync(string? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.ChatReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(r => r.Status.ToString() == normalized);
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(r => new
            {
                r.Id,
                r.MessageId,
                r.ReporterUserId,
                ReporterName = dbContext.Users.Where(u => u.Id == r.ReporterUserId).Select(u => u.DisplayName).FirstOrDefault(),
                r.Reason,
                Status = r.Status.ToString(),
                r.ResolvedByAdminUserId,
                r.ResolutionNote,
                r.ResolvedAt,
                r.CreatedAt,
                Message = r.Message == null ? null : new ChatMessageRow(
                    r.Message.Id,
                    r.Message.ConversationId,
                    r.Message.SenderUserId,
                    r.Message.SenderUserId == Guid.Empty ? "Hệ thống" : r.Message.SenderUser.DisplayName,
                    r.Message.SenderUserId == Guid.Empty
                        ? null
                        : (dbContext.StudentProfiles.Where(s => s.UserId == r.Message.SenderUserId).Select(s => s.AvatarUrl).FirstOrDefault()
                            ?? dbContext.LecturerProfiles.Where(l => l.UserId == r.Message.SenderUserId).Select(l => l.AvatarUrl).FirstOrDefault()
                            ?? dbContext.CompanyMembers.Where(c => c.UserId == r.Message.SenderUserId).Select(c => c.Company.LogoUrl).FirstOrDefault()),
                    r.Message.MessageType,
                    r.Message.Content,
                    r.Message.MetadataJson,
                    r.Message.ReplyToMessageId,
                    r.Message.ReplyTo != null ? r.Message.ReplyTo.Content : null,
                    r.Message.ReplyTo != null && r.Message.ReplyTo.SenderUserId != Guid.Empty ? r.Message.ReplyTo.SenderUser.DisplayName : "Hệ thống",
                    r.Message.IsEdited,
                    r.Message.EditedAt,
                    r.Message.IsDeleted,
                    r.Message.CreatedAt),
            })
            .ToListAsync(cancellationToken);

        var result = new List<ChatReportAdminRow>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new ChatReportAdminRow(
                row.Id,
                row.MessageId,
                row.ReporterUserId,
                row.ReporterName ?? string.Empty,
                row.Reason,
                row.Status,
                row.ResolvedByAdminUserId,
                row.ResolutionNote,
                row.ResolvedAt,
                row.CreatedAt,
                row.Message));
        }

        return result;
    }

    public Task<int> CountReportsAsync(string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.ChatReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(r => r.Status.ToString() == normalized);
        }

        return query.CountAsync(cancellationToken);
    }

    public async Task BanChatAsync(Guid targetUserId, string? reason, DateTimeOffset? until, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (user is null) return;
        user.BanChat(reason, until);
    }

    public async Task UnbanChatAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (user is null) return;
        user.UnbanChat();
    }

    public Task<int> CountChatBannedUsersAsync(CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.ChatBanReason != null && (u.ChatBannedUntil == null || u.ChatBannedUntil > DateTimeOffset.UtcNow))
            .CountAsync(cancellationToken);

    public Task<int> CountAllConversationsAsync(CancellationToken cancellationToken) =>
        dbContext.Conversations.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountAllMessagesAsync(CancellationToken cancellationToken) =>
        dbContext.ChatMessages.AsNoTracking().CountAsync(cancellationToken);

    // ---------- Helpers: role/avatar resolution ----------

    private async Task<string?> ResolveProfileAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        var studentAvatar = await dbContext.StudentProfiles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.AvatarUrl)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(studentAvatar)) return studentAvatar;

        var lecturerAvatar = await dbContext.LecturerProfiles.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => l.AvatarUrl)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(lecturerAvatar)) return lecturerAvatar;

        return await dbContext.CompanyMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.Company.LogoUrl)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> ResolveProfileRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await dbContext.StudentProfiles.AsNoTracking().AnyAsync(s => s.UserId == userId, cancellationToken)) return RoleNames.Student;
        if (await dbContext.LecturerProfiles.AsNoTracking().AnyAsync(l => l.UserId == userId, cancellationToken)) return RoleNames.Lecturer;
        if (await dbContext.CompanyMembers.AsNoTracking().AnyAsync(m => m.UserId == userId, cancellationToken)) return RoleNames.Company;
        return RoleNames.Student;
    }

    private async Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

    // ---------- Projection types ----------

    private sealed class InboxRow
    {
        public Guid Id { get; set; }
        public ConversationType Type { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ProjectTitle { get; set; }
        public string? Title { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastMessageAt { get; set; }
        public Guid? LastMessageId { get; set; }
        public string? LastMessagePreview { get; set; }
        public Guid LastMessageSenderUserId { get; set; }
        public string? LastMessageSenderName { get; set; }
        public ChatMessageType LastMessageType { get; set; }
        public DateTimeOffset? LastMessageCreatedAt { get; set; }
        public int ParticipantsCount { get; set; }
        public bool IsMuted { get; set; }
        public bool IsPinned { get; set; }
        public ConversationRequestStatus RequestStatus { get; set; }
    }
}
