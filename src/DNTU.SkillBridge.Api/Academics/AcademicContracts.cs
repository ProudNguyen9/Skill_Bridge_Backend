using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Academics;

namespace DNTU.SkillBridge.Api.Academics;

public sealed class CreateCourseRequest
{
    [Required, StringLength(32, MinimumLength = 2)] public string Code { get; init; } = string.Empty;
    [Required, StringLength(300, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
}

public sealed class CreateRubricRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
}

public sealed class CreateRubricCriterionRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "100")] public decimal Weight { get; init; }
    [Range(1, 100)] public int SortOrder { get; init; }
}

public sealed class UpdateCourseRequest
{
    [Required, StringLength(32, MinimumLength = 2)] public string Code { get; init; } = string.Empty;
    [Required, StringLength(300, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
}

public sealed class UpdateRubricCriterionRequest
{
    [Required, StringLength(300, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "100")] public decimal Weight { get; init; }
    [Range(1, 100)] public int SortOrder { get; init; }
}

public sealed class UpsertCourseProjectRequest
{
    [Required] public Guid CourseId { get; init; }
    [Required] public Guid ProjectId { get; init; }
    [Required] public Guid RubricId { get; init; }
}

public sealed record CourseResponse(Guid Id, string Code, string Name, bool IsActive);
public sealed record CourseProjectResponse(Guid Id, Guid CourseId, Guid ProjectId, Guid RubricId, string Status, DateTimeOffset? ApprovedAt);
public sealed record RubricResponse(Guid Id, Guid LecturerId, string Name, bool IsLocked, IReadOnlyCollection<RubricCriterionResponse> Criteria);
public sealed record RubricCriterionResponse(Guid Id, Guid RubricId, string Name, decimal Weight, int SortOrder);
