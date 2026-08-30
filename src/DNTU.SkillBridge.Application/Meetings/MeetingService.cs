namespace DNTU.SkillBridge.Application.Meetings;

public interface IMeetingService : IMeetingRepository;

public sealed class MeetingService(IMeetingRepository meetingRepository) : IMeetingService
{
    public Task<MeetingResponse?> CreateAsync(Guid userId, Guid projectId, CreateMeetingRequest request, CancellationToken cancellationToken) =>
        meetingRepository.CreateAsync(userId, projectId, request, cancellationToken);

    public Task<IReadOnlyCollection<MeetingResponse>?> ListAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        meetingRepository.ListAsync(userId, projectId, cancellationToken);

    public Task<MeetingResponse?> GetAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken) =>
        meetingRepository.GetAsync(userId, meetingId, cancellationToken);

    public Task<MeetingResponse?> UpdateAsync(Guid userId, Guid meetingId, UpdateMeetingRequest request, CancellationToken cancellationToken) =>
        meetingRepository.UpdateAsync(userId, meetingId, request, cancellationToken);

    public Task<bool> DeleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken) =>
        meetingRepository.DeleteAsync(userId, meetingId, cancellationToken);

    public Task<IReadOnlyCollection<MeetingParticipantResponse>?> GetParticipantsAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken) =>
        meetingRepository.GetParticipantsAsync(userId, meetingId, cancellationToken);

    public Task<bool> UpdateAttendanceAsync(Guid userId, Guid meetingId, UpdateAttendanceRequest request, CancellationToken cancellationToken) =>
        meetingRepository.UpdateAttendanceAsync(userId, meetingId, request, cancellationToken);

    public Task<(MeetingOutcome Outcome, MeetingMinutesResponse? Minutes)> GetMinutesAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken) =>
        meetingRepository.GetMinutesAsync(userId, meetingId, cancellationToken);

    public Task<(MeetingOutcome Outcome, MeetingMinutesResponse? Minutes)> UpsertMinutesAsync(Guid userId, Guid meetingId, UpsertMeetingMinutesRequest request, CancellationToken cancellationToken) =>
        meetingRepository.UpsertMinutesAsync(userId, meetingId, request, cancellationToken);

    public Task<(MeetingOutcome Outcome, MeetingActionItemResponse? ActionItem)> CreateActionItemAsync(Guid userId, Guid meetingId, CreateMeetingActionItemRequest request, CancellationToken cancellationToken) =>
        meetingRepository.CreateActionItemAsync(userId, meetingId, request, cancellationToken);

    public Task<(MeetingOutcome Outcome, MeetingActionItemResponse? ActionItem)> UpdateActionItemAsync(Guid userId, Guid actionItemId, UpdateMeetingActionItemRequest request, CancellationToken cancellationToken) =>
        meetingRepository.UpdateActionItemAsync(userId, actionItemId, request, cancellationToken);

    public Task<MeetingOutcome> DeleteActionItemAsync(Guid userId, Guid actionItemId, Guid version, CancellationToken cancellationToken) =>
        meetingRepository.DeleteActionItemAsync(userId, actionItemId, version, cancellationToken);

    public Task<(MeetingOutcome Outcome, MeetingActionItemConversionResponse? Conversion)> ConvertActionItemToTaskAsync(Guid userId, Guid actionItemId, ConvertMeetingActionItemRequest request, CancellationToken cancellationToken) =>
        meetingRepository.ConvertActionItemToTaskAsync(userId, actionItemId, request, cancellationToken);

    public Task<IReadOnlyCollection<MeetingResponse>> ListStudentMeetingsAsync(Guid userId, CancellationToken cancellationToken) =>
        meetingRepository.ListStudentMeetingsAsync(userId, cancellationToken);

    public Task<IReadOnlyCollection<MeetingResponse>> ListCompanyMeetingsAsync(Guid userId, CancellationToken cancellationToken) =>
        meetingRepository.ListCompanyMeetingsAsync(userId, cancellationToken);

    public Task<IReadOnlyCollection<MeetingResponse>> ListLecturerMeetingsAsync(Guid userId, CancellationToken cancellationToken) =>
        meetingRepository.ListLecturerMeetingsAsync(userId, cancellationToken);
}
