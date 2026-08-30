using Asp.Versioning;
using DNTU.SkillBridge.Application.Academics;
using DNTU.SkillBridge.Application.Common;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNTU.SkillBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
[Produces("application/json")]
public sealed class AcademicsController(IAcademicService academicService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("admin/courses")]
    public async Task<ActionResult<ApiResponse<CourseResponse>>> CreateCourse(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var course = await academicService.CreateCourseAsync(request, cancellationToken);
        return course is null ? Conflict() : Created(string.Empty, new ApiResponse<CourseResponse>(course));
    }

    [HttpPut("admin/courses/{courseId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CourseResponse>>> UpdateCourse(Guid courseId, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var course = await academicService.UpdateCourseAsync(courseId, request, cancellationToken);
        return course is null ? Conflict() : Ok(new ApiResponse<CourseResponse>(course));
    }

    [HttpGet("courses")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CourseResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CourseResponse>>>> ListCourses(CancellationToken cancellationToken) =>
        Ok(new ApiResponse<IReadOnlyCollection<CourseResponse>>(await academicService.ListCoursesAsync(cancellationToken)));

    [HttpGet("admin/course-projects")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CourseProjectResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CourseProjectResponse>>>> ListCourseProjects(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<CourseProjectResponse>>(await academicService.ListCourseProjectsAsync(cancellationToken)));
    }

    [HttpPost("admin/course-projects")]
    [ProducesResponseType(typeof(ApiResponse<CourseProjectResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CourseProjectResponse>>> CreateCourseProject(UpsertCourseProjectRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var mapping = await academicService.CreateCourseProjectAsync(UserId, request, cancellationToken);
        return mapping is null ? Conflict() : Created(string.Empty, new ApiResponse<CourseProjectResponse>(mapping));
    }

    [HttpPut("admin/course-projects/{mappingId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CourseProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CourseProjectResponse>>> UpdateCourseProject(Guid mappingId, UpsertCourseProjectRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var mapping = await academicService.UpdateCourseProjectAsync(mappingId, request, cancellationToken);
        return mapping is null ? Conflict() : Ok(new ApiResponse<CourseProjectResponse>(mapping));
    }

    [HttpPost("admin/course-projects/{mappingId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<CourseProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CourseProjectResponse>>> ApproveCourseProject(Guid mappingId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var mapping = await academicService.ApproveCourseProjectAsync(mappingId, UserId, cancellationToken);
        return mapping is null ? Conflict() : Ok(new ApiResponse<CourseProjectResponse>(mapping));
    }

    [HttpDelete("admin/course-projects/{mappingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCourseProject(Guid mappingId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        return await academicService.DeleteCourseProjectAsync(mappingId, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("lecturer/rubrics")]
    public async Task<ActionResult<ApiResponse<RubricResponse>>> CreateRubric(CreateRubricRequest request, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var rubric = await academicService.CreateRubricAsync(UserId, request, cancellationToken);
        return rubric is null ? NotFound() : Created(string.Empty, new ApiResponse<RubricResponse>(rubric));
    }

    [HttpGet("lecturer/rubrics")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<RubricResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RubricResponse>>>> ListRubrics(CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        return Ok(new ApiResponse<IReadOnlyCollection<RubricResponse>>(await academicService.ListRubricsAsync(UserId, cancellationToken)));
    }

    [HttpGet("lecturer/rubrics/{rubricId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RubricResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RubricResponse>>> GetRubric(Guid rubricId, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var rubric = await academicService.GetRubricAsync(UserId, rubricId, cancellationToken);
        return rubric is null ? NotFound() : Ok(new ApiResponse<RubricResponse>(rubric));
    }

    [HttpPost("lecturer/rubrics/{rubricId:guid}/criteria")]
    public async Task<ActionResult<ApiResponse<RubricResponse>>> AddCriterion(Guid rubricId, CreateRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var rubric = await academicService.AddCriterionAsync(UserId, rubricId, request, cancellationToken);
        return rubric is null ? Conflict() : Ok(new ApiResponse<RubricResponse>(rubric));
    }

    [HttpPut("lecturer/rubric-criteria/{criterionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RubricResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RubricResponse>>> UpdateCriterion(Guid criterionId, UpdateRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var rubric = await academicService.UpdateCriterionAsync(UserId, criterionId, request, cancellationToken);
        return rubric is null ? Conflict() : Ok(new ApiResponse<RubricResponse>(rubric));
    }

    [HttpDelete("lecturer/rubric-criteria/{criterionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCriterion(Guid criterionId, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        return await academicService.DeleteCriterionAsync(UserId, criterionId, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("lecturer/rubrics/{rubricId:guid}/lock")]
    public async Task<ActionResult<ApiResponse<RubricResponse>>> Lock(Guid rubricId, CancellationToken cancellationToken)
    {
        if (!IsLecturer()) return Forbid();
        var rubric = await academicService.LockRubricAsync(UserId, rubricId, cancellationToken);
        return rubric is null ? Conflict() : Ok(new ApiResponse<RubricResponse>(rubric));
    }

    private Guid UserId => currentUser.UserId!.Value;
    private bool IsLecturer() => currentUser.Roles.Contains(RoleNames.Lecturer);
    private bool IsAdmin() => currentUser.Roles.Contains(RoleNames.Admin) || currentUser.Roles.Contains(RoleNames.SuperAdmin);
}
