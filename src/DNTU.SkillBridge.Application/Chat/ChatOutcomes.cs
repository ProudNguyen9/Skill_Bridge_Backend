namespace DNTU.SkillBridge.Application.Chat;

/// <summary>Kết quả nghiệp vụ của các thao tác chat — controller ánh xạ sang HTTP status.</summary>
public enum ChatOutcome
{
    Success = 0,
    ConversationNotFound = 1,
    MessageNotFound = 2,
    Forbidden = 3,
    BlockedByParticipant = 4,
    NotAccepted = 5,
    RateLimited = 6,
    InvalidOperation = 7,
    ChatBanned = 8,
    InvalidAttachment = 9,
    TargetNotFound = 10,
    TargetIsAdmin = 11,
    ProjectNotFound = 12,
}

public enum ChatReportOutcome
{
    Success = 0,
    MessageNotFound = 1,
    AlreadyReported = 2,
}
