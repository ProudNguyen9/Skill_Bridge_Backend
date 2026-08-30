using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Lecturers;

public enum LecturerAssignmentCreateOutcome
{
    Created,
    LecturerNotFound,
    LecturerInactive,
    DuplicateAssignment
}

public enum LecturerAssignmentAcceptOutcome
{
    Accepted,
    NotFound,
    NotOpenForAcceptance
}

public sealed class LecturerService(AppDbContext dbContext)
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return ProjectProfile(profile);
    }

    /// <summary>Lists the caller's project supervision assignments, oldest first.</summary>
    public async Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetMyAssignmentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        return await ProjectAssignmentsAsync(dbContext, profile.Id, cancellationToken);
    }

    /// <summary>The caller accepts an invited supervision assignment as their own.</summary>
    public async Task<(LecturerAssignmentAcceptOutcome Outcome, LecturerAssignmentResponse? Assignment)> AcceptMyAssignmentAsync(
        Guid userId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        var assignment = await dbContext.LecturerAssignments
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == assignmentId && item.LecturerId == profile.Id, cancellationToken);
        if (assignment is null)
        {
            return (LecturerAssignmentAcceptOutcome.NotFound, null);
        }

        if (assignment.Status != LecturerAssignmentStatus.INVITED)
        {
            return (LecturerAssignmentAcceptOutcome.NotOpenForAcceptance, null);
        }

        assignment.Accept(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (LecturerAssignmentAcceptOutcome.Accepted, ProjectAssignment(assignment));
    }

    /// <summary>Admin projection of one lecturer's supervision assignments.</summary>
    public async Task<IReadOnlyCollection<LecturerAssignmentResponse>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        await ProjectAssignmentsAsync(dbContext, lecturerId, cancellationToken);

    /// <summary>
    /// Invites a lecturer to supervise a project (status INVITED). Only existing active lecturers qualify and a
    /// project can hold a single assignment row for now, so any existing assignment for the project conflicts.
    /// </summary>
    public async Task<(LecturerAssignmentCreateOutcome Outcome, LecturerAssignmentResponse? Assignment)> CreateAssignmentAsync(
        Guid lecturerId, CreateLecturerAssignmentRequest request, CancellationToken cancellationToken)
    {
        var profile = await dbContext.LecturerProfiles
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == lecturerId, cancellationToken);
        if (profile is null)
        {
            return (LecturerAssignmentCreateOutcome.LecturerNotFound, null);
        }

        if (!profile.CanTakeAssignments())
        {
            return (LecturerAssignmentCreateOutcome.LecturerInactive, null);
        }

        var duplicate = await dbContext.LecturerAssignments
            .AnyAsync(item => item.ProjectId == request.ProjectId, cancellationToken);
        if (duplicate)
        {
            return (LecturerAssignmentCreateOutcome.DuplicateAssignment, null);
        }

        var assignment = profile.AssignProject(request.ProjectId, request.Role, request.Note);
        dbContext.LecturerAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (LecturerAssignmentCreateOutcome.Created, ProjectAssignment(assignment));
    }

    /// <summary>Removes a supervision assignment; returns false when it does not exist.</summary>
    public async Task<bool> DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await dbContext.LecturerAssignments
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        dbContext.LecturerAssignments.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<LecturerProfile> EnsureProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the context defaults to NoTracking and /me writes mutate this entity.
        var profile = await dbContext.LecturerProfiles
            .AsTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new LecturerProfile(userId);
        dbContext.LecturerProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
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

    private static async Task<IReadOnlyCollection<LecturerAssignmentResponse>> ProjectAssignmentsAsync(
        AppDbContext dbContext, Guid lecturerId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.LecturerAssignments
            .AsNoTracking()
            .Where(item => item.LecturerId == lecturerId)
            .OrderBy(item => item.AssignedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ProjectAssignment).ToList();
    }
}
