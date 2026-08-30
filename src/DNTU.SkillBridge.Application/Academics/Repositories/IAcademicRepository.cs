using DNTU.SkillBridge.Domain.Academics;

namespace DNTU.SkillBridge.Application.Academics;

/// <summary>Data operations for courses, course-project mappings, and grading rubrics.</summary>
public interface IAcademicRepository
{
    // Courses
    Task<IReadOnlyCollection<Course>> ListActiveCoursesAsync(CancellationToken cancellationToken);

    Task<bool> HasCourseWithCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> HasOtherCourseWithCodeAsync(Guid courseId, string code, CancellationToken cancellationToken);

    /// <summary>Reads a course with the context's default tracking behavior.</summary>
    Task<Course?> FindCourseAsync(Guid courseId, CancellationToken cancellationToken);

    void AddCourse(Course course);

    // Course-project mappings
    Task<IReadOnlyCollection<CourseProject>> ListCourseProjectsAsync(CancellationToken cancellationToken);

    Task<bool> HasNonRemovedCourseProjectForProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Reads a course-project mapping as a tracked entity so mutations are persisted.</summary>
    Task<CourseProject?> FindCourseProjectForUpdateAsync(Guid mappingId, CancellationToken cancellationToken);

    void AddCourseProject(CourseProject mapping);

    // Lecturer profile lookups
    Task<Guid?> FindActiveLecturerIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> IsActiveLecturerOwnerAsync(Guid userId, Guid lecturerId, CancellationToken cancellationToken);

    // Rubrics
    Task<IReadOnlyCollection<Rubric>> ListRubricsWithCriteriaAsync(Guid lecturerId, CancellationToken cancellationToken);

    /// <summary>Reads a rubric with its criteria with the context's default tracking behavior.</summary>
    Task<Rubric?> FindRubricWithCriteriaAsync(Guid rubricId, CancellationToken cancellationToken);

    Task<Rubric?> FindRubricWithCriteriaNoTrackingAsync(Guid rubricId, CancellationToken cancellationToken);

    Task<Rubric?> FindRubricWithCriteriaByCriterionIdAsync(Guid criterionId, CancellationToken cancellationToken);

    void AddRubric(Rubric rubric);

    Task<bool> HasActiveCourseAsync(Guid courseId, CancellationToken cancellationToken);

    Task<bool> HasLockedRubricAsync(Guid rubricId, CancellationToken cancellationToken);

    Task<bool> HasActiveProjectAsync(Guid projectId, CancellationToken cancellationToken);
}
