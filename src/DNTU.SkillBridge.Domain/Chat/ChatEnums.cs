namespace DNTU.SkillBridge.Domain.Chat;

/// <summary>Loại hội thoại: 1-1 hoặc nhóm dự án 3 bên.</summary>
public enum ConversationType
{
    DIRECT = 1,
    PROJECT_GROUP = 2,
}

/// <summary>Loại nội dung của một tin nhắn chat.</summary>
public enum ChatMessageType
{
    TEXT = 1,
    IMAGE = 2,
    FILE = 3,
    CONTEXT_CARD = 4,
    SYSTEM = 5,
}

/// <summary>Vai trò của thành viên trong hội thoại.</summary>
public enum ConversationParticipantRole
{
    OWNER = 1,
    MEMBER = 2,
}

/// <summary>Trạng thái tin nhắn chờ: người nhận chưa chấp nhận hội thoại từ người lạ.</summary>
public enum ConversationRequestStatus
{
    ACCEPTED = 1,
    PENDING_REQUEST = 2,
    DECLINED = 3,
}

/// <summary>Trạng thái xử lý của một báo cáo vi phạm chat.</summary>
public enum ChatReportStatus
{
    OPEN = 1,
    RESOLVED = 2,
    DISMISSED = 3,
}

/// <summary>Hành động xử lý báo cáo của quản trị viên.</summary>
public enum ChatReportDecision
{
    DISMISSED = 1,
    WARNED = 2,
    BANNED = 3,
}
