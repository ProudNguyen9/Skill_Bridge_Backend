using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Lecturers;

namespace DNTU.SkillBridge.Application.Lecturers;

public interface ILecturerService
{
    Task<LecturerProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<LecturerProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateLecturerProfileRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId, CancellationToken cancellationToken);

    Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> AcceptMyAssignmentAsync(
        Guid userId, Guid assignmentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken);

    Task<(LecturerAssignmentCreateOutcome Outcome, LecturerAssignmentResponse? Assignment)> CreateAssignmentAsync(
        Guid lecturerId, CreateLecturerAssignmentRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);
}

public sealed class LecturerService(ILecturerRepository lecturerRepository, IUnitOfWork unitOfWork) : ILecturerService
{
    /// <summary>Returns the caller's own profile, lazily creating it (active, private) on first access.</summary>
    public async Task<LecturerProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return ProjectProfile(profile);
    }

    /// <summary>Updates the caller's own profile. The lecturer identity always comes from the session.</summary>
    public async Task<LecturerProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateLecturerProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        profile.Update(
            request.LecturerCode,
            request.Department,
            request.AcademicTitle,
            request.Bio,
            request.WebsiteUrl,
            request.OfficeLocation,
            request.PhoneNumber,
            request.IsProfilePublic,
            request.ShowContactInfo);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProjectProfile(profile);
    }

    /// <summary>Lists the caller's project supervision assignments, oldest first.</summary>
    public async Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return ProjectAssignments(await lecturerRepository.ListAssignmentsAsync(profile.Id, cancellationToken));
    }

    /// <summary>The caller accepts an invited supervision assignment as their own.</summary>
    public async Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> AcceptMyAssignmentAsync(
        Guid userId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var assignment = await lecturerRepository.FindAssignmentForUpdateAsync(assignmentId, profile.Id, cancellationToken);
        if (assignment is null)
        {
            return (LecturerAssignmentAcceptOutcome.NotFound, null);
        }

        if (assignment.Status != LecturerAssignmentStatus.INVITED)
        {
            return (LecturerAssignmentAcceptOutcome.NotOpenForAcceptance, null);
        }

        assignment.Accept(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (LecturerAssignmentAcceptOutcome.Accepted, ProjectAssignment(assignment));
    }

    /// <summary>Admin projection of one lecturer's supervision assignments.</summary>
    public async Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        ProjectAssignments(await lecturerRepository.ListAssignmentsAsync(lecturerId, cancellationToken));

    /// <summary>
    /// Invites a lecturer to supervise a project (status INVITED). Only existing active lecturers qualify and a
    /// project can hold a single assignment row for now, so any existing assignment for the project conflicts.
    /// </summary>
    public async Task<(LecturerAssignmentCreateOutcome Outcome, LecturerAssignmentResponse? Assignment)> CreateAssignmentAsync(
        Guid lecturerId, CreateLecturerAssignmentRequest request, CancellationToken cancellationToken)
    {
        var profile = await lecturerRepository.FindProfileForUpdateAsync(lecturerId, cancellationToken);
        if (profile is null)
        {
            return (LecturerAssignmentCreateOutcome.LecturerNotFound, null);
        }

        if (!profile.CanTakeAssignments())
        {
            return (LecturerAssignmentCreateOutcome.LecturerInactive, null);
        }

        if (await lecturerRepository.HasAssignmentForProjectAsync(request.ProjectId, cancellationToken))
        {
            return (LecturerAssignmentCreateOutcome.DuplicateAssignment, null);
        }

        var assignment = profile.AssignProject(request.ProjectId, request.Role, request.Note);
        lecturerRepository.AddAssignment(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (LecturerAssignmentCreateOutcome.Created, ProjectAssignment(assignment));
    }

    /// <summary>Removes a supervision assignment; returns false when it does not exist.</summary>
    public async Task<bool> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await lecturerRepository.FindAssignmentAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        lecturerRepository.RemoveAssignment(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<LecturerProfile> EnsureProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the context defaults to NoTracking and /me writes mutate this entity.
        var profile = await lecturerRepository.FindProfileByUserIdAsync(userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new LecturerProfile(userId);
        lecturerRepository.AddProfile(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static LecturerProfileResponse ProjectProfile(LecturerProfile profile) =>
        new(profile.Id,
            profile.LecturerCode,
            profile.Department,
            profile.AcademicTitle,
            profile.Bio,
            profile.WebsiteUrl,
            profile.OfficeLocation,
            profile.PhoneNumber,
            profile.IsProfilePublic,
            profile.ShowContactInfo,
            profile.IsActive);

    private static LecturerAssignmentResponse ProjectAssignment(LecturerAssignment assignment) =>
        new(assignment.Id,
            assignment.LecturerId,
            assignment.ProjectId,
            assignment.Role.ToString(),
            assignment.Status.ToString(),
            assignment.AssignedAt,
            assignment.AcceptedAt,
            assignment.EndedAt,
            assignment.Note);

    private static IReadOnlyCollection<LecturerAssignmentResponse> ProjectAssignments(IReadOnlyCollection<LecturerAssignment> assignments) =>
        assignments.Select(ProjectAssignment).ToList();
}
