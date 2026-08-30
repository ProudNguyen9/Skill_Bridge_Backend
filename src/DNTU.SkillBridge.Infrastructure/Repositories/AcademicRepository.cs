using DNTU.SkillBridge.Application.Academics;
using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>EF Core implementation of the academic course, mapping, and rubric data operations.</summary>
public sealed class AcademicRepository(AppDbContext dbContext) : IAcademicRepository
{
    public async Task<IReadOnlyCollection<Course>> ListActiveCoursesAsync(CancellationToken cancellationToken) =>
        await dbContext.Courses.AsNoTracking()
            .Where(course => course.IsActive)
            .OrderBy(course => course.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> HasCourseWithCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Courses.AnyAsync(course => course.Code == code, cancellationToken);

    public Task<bool> HasOtherCourseWithCodeAsync(Guid courseId, string code, CancellationToken cancellationToken) =>
        dbContext.Courses.AnyAsync(item => item.Id != courseId && item.Code == code, cancellationToken);

    public Task<Course?> FindCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        dbContext.Courses.SingleOrDefaultAsync(item => item.Id == courseId, cancellationToken);

    public void AddCourse(Course course) => dbContext.Courses.Add(course);

    public async Task<IReadOnlyCollection<CourseProject>> ListCourseProjectsAsync(CancellationToken cancellationToken) =>
        await dbContext.CourseProjects.AsNoTracking()
            .OrderByDescending(mapping => mapping.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> HasNonRemovedCourseProjectForProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.CourseProjects.AnyAsync(mapping => mapping.ProjectId == projectId && mapping.Status != CourseProjectStatus.REMOVED, cancellationToken);

    public Task<CourseProject?> FindCourseProjectForUpdateAsync(Guid mappingId, CancellationToken cancellationToken) =>
        dbContext.CourseProjects.AsTracking().SingleOrDefaultAsync(item => item.Id == mappingId, cancellationToken);

    public void AddCourseProject(CourseProject mapping) => dbContext.CourseProjects.Add(mapping);

    public Task<Guid?> FindActiveLecturerIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsActiveLecturerOwnerAsync(Guid userId, Guid lecturerId, CancellationToken cancellationToken) =>
        dbContext.LecturerProfiles.AnyAsync(profile => profile.Id == lecturerId && profile.UserId == userId && profile.IsActive, cancellationToken);

    public async Task<IReadOnlyCollection<Rubric>> ListRubricsWithCriteriaAsync(Guid lecturerId, CancellationToken cancellationToken) =>
        await dbContext.Rubrics.AsNoTracking()
            .Include(item => item.Criteria)
            .Where(item => item.LecturerId == lecturerId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

    public Task<Rubric?> FindRubricWithCriteriaAsync(Guid rubricId, CancellationToken cancellationToken) =>
        dbContext.Rubrics.Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Id == rubricId, cancellationToken);

    public Task<Rubric?> FindRubricWithCriteriaNoTrackingAsync(Guid rubricId, CancellationToken cancellationToken) =>
        dbContext.Rubrics.AsNoTracking()
            .Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Id == rubricId, cancellationToken);

    public Task<Rubric?> FindRubricWithCriteriaByCriterionIdAsync(Guid criterionId, CancellationToken cancellationToken) =>
        dbContext.Rubrics
            .Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.Criteria.Any(criterion => criterion.Id == criterionId), cancellationToken);

    public void AddRubric(Rubric rubric) => dbContext.Rubrics.Add(rubric);

    public Task<bool> HasActiveCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        dbContext.Courses.AnyAsync(course => course.Id == courseId && course.IsActive, cancellationToken);

    public Task<bool> HasLockedRubricAsync(Guid rubricId, CancellationToken cancellationToken) =>
        dbContext.Rubrics.AnyAsync(rubric => rubric.Id == rubricId && rubric.IsLocked, cancellationToken);

    public Task<bool> HasActiveProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == projectId && project.IsActive, cancellationToken);
}
