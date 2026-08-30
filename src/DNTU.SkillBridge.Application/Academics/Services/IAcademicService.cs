using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Domain.Academics;

namespace DNTU.SkillBridge.Application.Academics;

public interface IAcademicService
{
    Task<IReadOnlyCollection<CourseResponse>> ListCoursesAsync(CancellationToken cancellationToken);

    Task<CourseResponse?> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken);

    Task<CourseResponse?> UpdateCourseAsync(Guid courseId, UpdateCourseRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CourseProjectResponse>> ListCourseProjectsAsync(CancellationToken cancellationToken);

    Task<CourseProjectResponse?> CreateCourseProjectAsync(Guid userId, UpsertCourseProjectRequest request, CancellationToken cancellationToken);

    Task<CourseProjectResponse?> UpdateCourseProjectAsync(Guid mappingId, UpsertCourseProjectRequest request, CancellationToken cancellationToken);

    Task<CourseProjectResponse?> ApproveCourseProjectAsync(Guid mappingId, Guid approvedByUserId, CancellationToken cancellationToken);

    Task<bool> DeleteCourseProjectAsync(Guid mappingId, CancellationToken cancellationToken);

    Task<RubricResponse?> CreateRubricAsync(Guid lecturerUserId, CreateRubricRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RubricResponse>> ListRubricsAsync(Guid lecturerUserId, CancellationToken cancellationToken);

    Task<RubricResponse?> GetRubricAsync(Guid lecturerUserId, Guid rubricId, CancellationToken cancellationToken);

    Task<RubricResponse?> AddCriterionAsync(Guid lecturerUserId, Guid rubricId, CreateRubricCriterionRequest request, CancellationToken cancellationToken);

    Task<RubricResponse?> UpdateCriterionAsync(Guid lecturerUserId, Guid criterionId, UpdateRubricCriterionRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteCriterionAsync(Guid lecturerUserId, Guid criterionId, CancellationToken cancellationToken);

    Task<RubricResponse?> LockRubricAsync(Guid lecturerUserId, Guid rubricId, CancellationToken cancellationToken);
}
