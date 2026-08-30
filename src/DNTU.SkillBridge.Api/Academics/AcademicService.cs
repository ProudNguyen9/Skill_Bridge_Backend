using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Api.Academics;

public sealed class AcademicService(AppDbContext dbContext)
{
    public async Task<IReadOnlyCollection<CourseResponse>> ListCoursesAsync(CancellationToken cancellationToken) =>
        await dbContext.Courses.AsNoTracking()
            .Where(course => course.IsActive)
            .OrderBy(course => course.Code)
            .Select(course => new CourseResponse(course.Id, course.Code, course.Name, course.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<CourseResponse?> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        if (await dbContext.Courses.AnyAsync(course => course.Code == request.Code.Trim(), cancellationToken)) return null;
        var course = new Course(request.Code, request.Name);
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CourseResponse(course.Id, course.Code, course.Name, course.IsActive);
    }

    public async Task<CourseResponse?> UpdateCourseAsync(Guid courseId, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = await dbContext.Courses.SingleOrDefaultAsync(item => item.Id == courseId, cancellationToken);
        if (course is null || await dbContext.Courses.AnyAsync(item => item.Id != courseId && item.Code == request.Code.Trim(), cancellationToken))
        {
            return null;
        }

        try
        {
            course.Update(request.Code, request.Name);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return new CourseResponse(course.Id, course.Code, course.Name, course.IsActive);
    }

    public async Task<IReadOnlyCollection<CourseProjectResponse>> ListCourseProjectsAsync(CancellationToken cancellationToken) =>
        await dbContext.CourseProjects.AsNoTracking()
            .OrderByDescending(mapping => mapping.CreatedAt)
            .Select(mapping => Map(mapping))
            .ToListAsync(cancellationToken);

    public async Task<CourseProjectResponse?> CreateCourseProjectAsync(Guid userId, UpsertCourseProjectRequest request, CancellationToken cancellationToken)
    {
        if (!await IsValidCourseProjectRequestAsync(request, cancellationToken)) return null;
        if (await dbContext.CourseProjects.AnyAsync(mapping => mapping.ProjectId == request.ProjectId && mapping.Status != CourseProjectStatus.REMOVED, cancellationToken)) return null;

        var mapping = new CourseProject(request.CourseId, request.ProjectId, request.RubricId, userId);
        dbContext.CourseProjects.Add(mapping);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(mapping);
    }

    public async Task<CourseProjectResponse?> UpdateCourseProjectAsync(Guid mappingId, UpsertCourseProjectRequest request, CancellationToken cancellationToken)
    {
        if (!await IsValidCourseProjectRequestAsync(request, cancellationToken)) return null;
        var mapping = await dbContext.CourseProjects.AsTracking().SingleOrDefaultAsync(item => item.Id == mappingId, cancellationToken);
        if (mapping is null || mapping.ProjectId != request.ProjectId) return null;

        try { mapping.Update(request.CourseId, request.RubricId); }
        catch (InvalidOperationException) { return null; }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(mapping);
    }

    public async Task<CourseProjectResponse?> ApproveCourseProjectAsync(Guid mappingId, Guid approvedByUserId, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.CourseProjects.AsTracking().SingleOrDefaultAsync(item => item.Id == mappingId, cancellationToken);
        if (mapping is null) return null;
        try { mapping.Approve(approvedByUserId, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return null; }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(mapping);
    }

    public async Task<bool> DeleteCourseProjectAsync(Guid mappingId, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.CourseProjects.AsTracking().SingleOrDefaultAsync(item => item.Id == mappingId, cancellationToken);
        if (mapping is null) return false;
        mapping.Remove();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RubricResponse?> CreateRubricAsync(Guid lecturerUserId, CreateRubricRequest request, CancellationToken cancellationToken)
    {
        var lecturerId = await dbContext.LecturerProfiles.AsNoTracking()
            .Where(profile => profile.UserId == lecturerUserId && profile.IsActive)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!lecturerId.HasValue) return null;

        var rubric = new Rubric(lecturerId.Value, request.Name);
        dbContext.Rubrics.Add(rubric);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    public async Task<IReadOnlyCollection<RubricResponse>> ListRubricsAsync(Guid lecturerUserId, CancellationToken cancellationToken)
    {
        var lecturerId = await LecturerIdAsync(lecturerUserId, cancellationToken);
        if (!lecturerId.HasValue) return [];
        return await dbContext.Rubrics.AsNoTracking()
            .Include(item => item.Criteria)
            .Where(item => item.LecturerId == lecturerId.Value)
            .OrderBy(item => item.Name)
            .Select(item => Map(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<RubricResponse?> GetRubricAsync(Guid lecturerUserId, Guid rubricId, CancellationToken cancellationToken)
    {
        var rubric = await dbContext.Rubrics.AsNoTracking()
            .Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Id == rubricId, cancellationToken);
        return rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken) ? null : Map(rubric);
    }

    public async Task<RubricResponse?> AddCriterionAsync(Guid lecturerUserId, Guid rubricId, CreateRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var rubric = await dbContext.Rubrics.Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Id == rubricId, cancellationToken);
        if (rubric is null || rubric.IsLocked || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken)) return null;
        try { rubric.AddCriterion(request.Name, request.Weight, request.SortOrder); }
        catch (InvalidOperationException) { return null; }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    public async Task<RubricResponse?> UpdateCriterionAsync(Guid lecturerUserId, Guid criterionId, UpdateRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var rubric = await dbContext.Rubrics
            .Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Criteria.Any(criterion => criterion.Id == criterionId), cancellationToken);
        if (rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken))
        {
            return null;
        }

        try
        {
            rubric.UpdateCriterion(criterionId, request.Name, request.Weight, request.SortOrder);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return Map(rubric);
    }

    public async Task<bool> DeleteCriterionAsync(Guid lecturerUserId, Guid criterionId, CancellationToken cancellationToken)
    {
        var rubric = await dbContext.Rubrics
            .Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Criteria.Any(criterion => criterion.Id == criterionId), cancellationToken);
        if (rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken))
        {
            return false;
        }

        try
        {
            rubric.RemoveCriterion(criterionId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<RubricResponse?> LockRubricAsync(Guid lecturerUserId, Guid rubricId, CancellationToken cancellationToken)
    {
        var rubric = await dbContext.Rubrics.Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Id == rubricId, cancellationToken);
        if (rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken)) return null;
        try { rubric.Lock(); }
        catch (InvalidOperationException) { return null; }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    private Task<bool> IsOwnerAsync(Guid userId, Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AnyAsync(profile => profile.Id == lecturerId && profile.UserId == userId && profile.IsActive, cancellationToken);

    private Task<Guid?> LecturerIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> IsValidCourseProjectRequestAsync(UpsertCourseProjectRequest request, CancellationToken cancellationToken) =>
        await dbContext.Courses.AnyAsync(course => course.Id == request.CourseId && course.IsActive, cancellationToken) &&
        await dbContext.Projects.AnyAsync(project => project.Id == request.ProjectId && project.IsActive, cancellationToken) &&
        await dbContext.Rubrics.AnyAsync(rubric => rubric.Id == request.RubricId && rubric.IsLocked, cancellationToken);

    private static RubricResponse Map(Rubric rubric) => new(rubric.Id, rubric.LecturerId, rubric.Name, rubric.IsLocked,
        rubric.Criteria.OrderBy(item => item.SortOrder).Select(item => new RubricCriterionResponse(item.Id, item.RubricId, item.Name, item.Weight, item.SortOrder)).ToList());

    private static CourseProjectResponse Map(CourseProject mapping) => new(mapping.Id, mapping.CourseId, mapping.ProjectId, mapping.RubricId, mapping.Status.ToString(), mapping.ApprovedAt);
}
