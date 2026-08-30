namespace DNTU.SkillBridge.Application.Meetings;

public interface IMeetingService
{
    Task<MeetingResponse?> CreateAsync(Guid userId, Guid projectId, CreateMeetingRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MeetingResponse>?> ListAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
    Task<MeetingResponse?> GetAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);
    Task<MeetingResponse?> UpdateAsync(Guid userId, Guid meetingId, UpdateMeetingRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MeetingParticipantResponse>?> GetParticipantsAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);
    Task<bool> UpdateAttendanceAsync(Guid userId, Guid meetingId, UpdateAttendanceRequest request, CancellationToken cancellationToken);
    Task<(MeetingOutcome Outcome, MeetingMinutesResponse? Minutes)> GetMinutesAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);
    Task<(MeetingOutcome Outcome, MeetingMinutesResponse? Minutes)> UpsertMinutesAsync(Guid userId, Guid meetingId, UpsertMeetingMinutesRequest request, CancellationToken cancellationToken);
    Task<(MeetingOutcome Outcome, MeetingActionItemResponse? ActionItem)> CreateActionItemAsync(Guid userId, Guid meetingId, CreateMeetingActionItemRequest request, CancellationToken cancellationToken);
    Task<(MeetingOutcome Outcome, MeetingActionItemResponse? ActionItem)> UpdateActionItemAsync(Guid userId, Guid actionItemId, UpdateMeetingActionItemRequest request, CancellationToken cancellationToken);
    Task<MeetingOutcome> DeleteActionItemAsync(Guid userId, Guid actionItemId, Guid version, CancellationToken cancellationToken);
    Task<(MeetingOutcome Outcome, MeetingActionItemConversionResponse? Conversion)> ConvertActionItemToTaskAsync(Guid userId, Guid actionItemId, ConvertMeetingActionItemRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MeetingResponse>> ListStudentMeetingsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MeetingResponse>> ListCompanyMeetingsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MeetingResponse>> ListLecturerMeetingsAsync(Guid userId, CancellationToken cancellationToken);
}
