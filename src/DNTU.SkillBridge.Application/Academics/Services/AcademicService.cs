using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Academics;

namespace DNTU.SkillBridge.Application.Academics;

public sealed class AcademicService(IAcademicRepository academicRepository, IUnitOfWork unitOfWork) : IAcademicService
{
    public async Task<IReadOnlyCollection<CourseResponse>> ListCoursesAsync(CancellationToken cancellationToken) =>
        (await academicRepository.ListActiveCoursesAsync(cancellationToken))
            .Select(course => new CourseResponse(course.Id, course.Code, course.Name, course.IsActive))
            .ToList();

    public async Task<CourseResponse?> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        if (await academicRepository.HasCourseWithCodeAsync(request.Code.Trim(), cancellationToken)) return null;
        var course = new Course(request.Code, request.Name);
        academicRepository.AddCourse(course);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CourseResponse(course.Id, course.Code, course.Name, course.IsActive);
    }

    public async Task<CourseResponse?> UpdateCourseAsync(Guid courseId, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = await academicRepository.FindCourseAsync(courseId, cancellationToken);
        if (course is null || await academicRepository.HasOtherCourseWithCodeAsync(courseId, request.Code.Trim(), cancellationToken))
        {
            return null;
        }

        try
        {
            course.Update(request.Code, request.Name);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return new CourseResponse(course.Id, course.Code, course.Name, course.IsActive);
    }

    public async Task<IReadOnlyCollection<CourseProjectResponse>> ListCourseProjectsAsync(CancellationToken cancellationToken) =>
        (await academicRepository.ListCourseProjectsAsync(cancellationToken))
            .Select(Map)
            .ToList();

    public async Task<CourseProjectResponse?> CreateCourseProjectAsync(Guid userId, UpsertCourseProjectRequest request, CancellationToken cancellationToken)
    {
        if (!await IsValidCourseProjectRequestAsync(request, cancellationToken)) return null;
        if (await academicRepository.HasNonRemovedCourseProjectForProjectAsync(request.ProjectId, cancellationToken)) return null;

        var mapping = new CourseProject(request.CourseId, request.ProjectId, request.RubricId, userId);
        academicRepository.AddCourseProject(mapping);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(mapping);
    }

    public async Task<CourseProjectResponse?> UpdateCourseProjectAsync(Guid mappingId, UpsertCourseProjectRequest request, CancellationToken cancellationToken)
    {
        if (!await IsValidCourseProjectRequestAsync(request, cancellationToken)) return null;
        var mapping = await academicRepository.FindCourseProjectForUpdateAsync(mappingId, cancellationToken);
        if (mapping is null || mapping.ProjectId != request.ProjectId) return null;

        try { mapping.Update(request.CourseId, request.RubricId); }
        catch (InvalidOperationException) { return null; }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(mapping);
    }

    public async Task<CourseProjectResponse?> ApproveCourseProjectAsync(Guid mappingId, Guid approvedByUserId, CancellationToken cancellationToken)
    {
        var mapping = await academicRepository.FindCourseProjectForUpdateAsync(mappingId, cancellationToken);
        if (mapping is null) return null;
        try { mapping.Approve(approvedByUserId, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return null; }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(mapping);
    }

    public async Task<bool> DeleteCourseProjectAsync(Guid mappingId, CancellationToken cancellationToken)
    {
        var mapping = await academicRepository.FindCourseProjectForUpdateAsync(mappingId, cancellationToken);
        if (mapping is null) return false;
        mapping.Remove();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RubricResponse?> CreateRubricAsync(Guid lecturerUserId, CreateRubricRequest request, CancellationToken cancellationToken)
    {
        var lecturerId = await academicRepository.FindActiveLecturerIdAsync(lecturerUserId, cancellationToken);
        if (!lecturerId.HasValue) return null;

        var rubric = new Rubric(lecturerId.Value, request.Name);
        academicRepository.AddRubric(rubric);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    public async Task<IReadOnlyCollection<RubricResponse>> ListRubricsAsync(Guid lecturerUserId, CancellationToken cancellationToken)
    {
        var lecturerId = await LecturerIdAsync(lecturerUserId, cancellationToken);
        if (!lecturerId.HasValue) return [];
        return (await academicRepository.ListRubricsWithCriteriaAsync(lecturerId.Value, cancellationToken))
            .Select(Map)
            .ToList();
    }

    public async Task<RubricResponse?> GetRubricAsync(Guid lecturerUserId, Guid rubricId, CancellationToken cancellationToken)
    {
        var rubric = await academicRepository.FindRubricWithCriteriaNoTrackingAsync(rubricId, cancellationToken);
        return rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken) ? null : Map(rubric);
    }

    public async Task<RubricResponse?> AddCriterionAsync(Guid lecturerUserId, Guid rubricId, CreateRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var rubric = await academicRepository.FindRubricWithCriteriaAsync(rubricId, cancellationToken);
        if (rubric is null || rubric.IsLocked || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken)) return null;
        try { rubric.AddCriterion(request.Name, request.Weight, request.SortOrder); }
        catch (InvalidOperationException) { return null; }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    public async Task<RubricResponse?> UpdateCriterionAsync(Guid lecturerUserId, Guid criterionId, UpdateRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var rubric = await academicRepository.FindRubricWithCriteriaByCriterionIdAsync(criterionId, cancellationToken);
        if (rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken))
        {
            return null;
        }

        try
        {
            rubric.UpdateCriterion(criterionId, request.Name, request.Weight, request.SortOrder);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return Map(rubric);
    }

    public async Task<bool> DeleteCriterionAsync(Guid lecturerUserId, Guid criterionId, CancellationToken cancellationToken)
    {
        var rubric = await academicRepository.FindRubricWithCriteriaByCriterionIdAsync(criterionId, cancellationToken);
        if (rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken))
        {
            return false;
        }

        try
        {
            rubric.RemoveCriterion(criterionId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<RubricResponse?> LockRubricAsync(Guid lecturerUserId, Guid rubricId, CancellationToken cancellationToken)
    {
        var rubric = await academicRepository.FindRubricWithCriteriaAsync(rubricId, cancellationToken);
        if (rubric is null || !await IsOwnerAsync(lecturerUserId, rubric.LecturerId, cancellationToken)) return null;
        try { rubric.Lock(); }
        catch (InvalidOperationException) { return null; }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    private Task<bool> IsOwnerAsync(Guid userId, Guid lecturerId, CancellationToken cancellationToken) =>
        academicRepository.IsActiveLecturerOwnerAsync(userId, lecturerId, cancellationToken);

    private Task<Guid?> LecturerIdAsync(Guid userId, CancellationToken cancellationToken) =>
        academicRepository.FindActiveLecturerIdAsync(userId, cancellationToken);

    private async Task<bool> IsValidCourseProjectRequestAsync(UpsertCourseProjectRequest request, CancellationToken cancellationToken) =>
        await academicRepository.HasActiveCourseAsync(request.CourseId, cancellationToken) &&
        await academicRepository.HasActiveProjectAsync(request.ProjectId, cancellationToken) &&
        await academicRepository.HasLockedRubricAsync(request.RubricId, cancellationToken);

    private static RubricResponse Map(Rubric rubric) => new(rubric.Id, rubric.LecturerId, rubric.Name, rubric.IsLocked,
        rubric.Criteria.OrderBy(item => item.SortOrder).Select(item => new RubricCriterionResponse(item.Id, item.RubricId, item.Name, item.Weight, item.SortOrder)).ToList());

    private static CourseProjectResponse Map(CourseProject mapping) => new(mapping.Id, mapping.CourseId, mapping.ProjectId, mapping.RubricId, mapping.Status.ToString(), mapping.ApprovedAt);
}
