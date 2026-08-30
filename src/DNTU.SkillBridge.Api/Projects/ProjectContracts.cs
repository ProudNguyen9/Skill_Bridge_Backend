using DNTU.SkillBridge.Api.Contracts;
using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Projects;

namespace DNTU.SkillBridge.Api.Projects;

public sealed class CreateProjectRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; init; }

    [StringLength(4000)]
    public string? ProblemStatement { get; init; }

    [StringLength(4000)]
    public string? BusinessRequirements { get; init; }

    [StringLength(4000)]
    public string? TechnicalConstraints { get; init; }

    public Guid? IndustryId { get; init; }

    [Required]
    public ProjectDifficulty Difficulty { get; init; }

    [Required]
    public ProjectWorkType WorkType { get; init; }

    [Required, Range(1, 52)]
    public int DurationWeeks { get; init; }

    public DateTimeOffset? ApplicationDeadline { get; init; }

    [Required, Range(1, 20)]
    public int ExpectedStudentCount { get; init; }

    [Required, Range(1, 20)]
    public int MinTeamSize { get; init; }

    [Required, Range(1, 20)]
    public int MaxTeamSize { get; init; }

    [Range(0, 100_000_000)]
    public long? AllowanceAmount { get; init; }

    [StringLength(3)]
    public string? AllowanceCurrency { get; init; }

    public List<ProjectSkillItem> Skills { get; init; } = [];

    public List<ProjectDeliverableItem> Deliverables { get; init; } = [];
}

public sealed class UpdateProjectRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; init; }

    [StringLength(4000)]
    public string? ProblemStatement { get; init; }

    [StringLength(4000)]
    public string? BusinessRequirements { get; init; }

    [StringLength(4000)]
    public string? TechnicalConstraints { get; init; }

    public Guid? IndustryId { get; init; }

    [Required]
    public ProjectDifficulty Difficulty { get; init; }

    [Required]
    public ProjectWorkType WorkType { get; init; }

    [Required, Range(1, 52)]
    public int DurationWeeks { get; init; }

    public DateTimeOffset? ApplicationDeadline { get; init; }

    [Required, Range(1, 20)]
    public int ExpectedStudentCount { get; init; }

    [Required, Range(1, 20)]
    public int MinTeamSize { get; init; }

    [Required, Range(1, 20)]
    public int MaxTeamSize { get; init; }

    [Range(0, 100_000_000)]
    public long? AllowanceAmount { get; init; }

    [StringLength(3)]
    public string? AllowanceCurrency { get; init; }

    public List<ProjectSkillItem> Skills { get; init; } = [];

    public List<ProjectDeliverableItem> Deliverables { get; init; } = [];
}

public sealed class ProjectSkillItem
{
    [Required]
    public Guid SkillId { get; init; }

    [Required]
    public RequirementLevel RequirementLevel { get; init; }

    public bool IsRequired { get; init; } = true;
}

public sealed class ProjectDeliverableItem
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }
}

public sealed record CompanyProjectListItemResponse(
    Guid Id,
    string Code,
    string Title,
    string Slug,
    string Status,
    ProjectDifficulty Difficulty,
    ProjectWorkType WorkType,
    int DurationWeeks,
    DateTimeOffset? ApplicationDeadline,
    int ExpectedStudentCount,
    long? AllowanceAmount,
    string? AllowanceCurrency,
    DateTimeOffset CreatedAt);

public sealed record CompanyProjectDetailResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Title,
    string Slug,
    string Status,
    bool IsActive,
    string? Summary,
    string? ProblemStatement,
    string? BusinessRequirements,
    string? TechnicalConstraints,
    Guid? IndustryId,
    string? IndustryName,
    ProjectDifficulty Difficulty,
    ProjectWorkType WorkType,
    int DurationWeeks,
    DateTimeOffset? ApplicationDeadline,
    int ExpectedStudentCount,
    int MinTeamSize,
    int MaxTeamSize,
    long? AllowanceAmount,
    string? AllowanceCurrency,
    IReadOnlyCollection<ProjectSkillResponse> Skills,
    IReadOnlyCollection<ProjectDeliverableResponse> Deliverables,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PublishedAt);

public sealed record ProjectSkillResponse(
    Guid SkillId,
    string SkillCode,
    string SkillName,
    string RequirementLevel,
    bool IsRequired);

public sealed record ProjectDeliverableResponse(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder);

/// <summary>Safe draft-compatible projection until the full progress module (Task 17/27) arrives.</summary>
public sealed record CompanyProjectProgressResponse(
    Guid ProjectId,
    string Status,
    int DeliverableCount,
    int SkillCount,
    bool IsDraft);

/// <summary>Admin/company decision request; the note records rejection or change-request reasons.</summary>
public sealed class ApprovalDecisionRequest
{
    [StringLength(1000)]
    public string? Note { get; init; }
}

public sealed record ApprovalHistoryItem(
    Guid Id,
    string Decision,
    string? Note,
    Guid DecidedByUserId,
    DateTimeOffset DecidedAt);

public sealed record AdminApprovalListItemResponse(
    Guid ProjectId,
    string Title,
    string CompanyName,
    string Status,
    DateTimeOffset? SubmittedAt,
    string? LastDecision,
    DateTimeOffset? LastDecidedAt);

public sealed record AdminApprovalDetailResponse(
    Guid ProjectId,
    string Title,
    string Slug,
    string CompanyName,
    string Status,
    string Code,
    string? Summary,
    string? ProblemStatement,
    string Difficulty,
    string WorkType,
    int DurationWeeks,
    DateTimeOffset? ApplicationDeadline,
    int ExpectedStudentCount,
    int MinTeamSize,
    int MaxTeamSize,
    long? AllowanceAmount,
    string? AllowanceCurrency,
    int SkillCount,
    int DeliverableCount,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyCollection<ApprovalHistoryItem> History);

/// <summary>
/// Public browse filters; every filter is optional and combined with AND.
/// Unrecognized difficulty/workType/applicationStatus values are ignored; an
/// unknown sort key is rejected with 400 by the controller.
/// </summary>
public sealed class PublicProjectFilterQuery : PageQuery
{
    /// <summary>Case-insensitive contains match against project title or summary.</summary>
    public string? Search { get; init; }

    public Guid? SkillId { get; init; }

    public Guid? IndustryId { get; init; }

    /// <summary>ProjectDifficulty name (e.g. BEGINNER); invalid values are ignored.</summary>
    public string? Difficulty { get; init; }

    /// <summary>ProjectWorkType name (e.g. HYBRID); invalid values are ignored.</summary>
    public string? WorkType { get; init; }

    public int? DurationMin { get; init; }

    public int? DurationMax { get; init; }

    public long? AllowanceMin { get; init; }

    public long? AllowanceMax { get; init; }

    /// <summary>Optional public status filter (APPROVED/RECRUITING/IN_PROGRESS/COMPLETED); other values are ignored.</summary>
    public string? ApplicationStatus { get; init; }

    /// <summary>Named sort from the allow-list: newest (default), oldest, deadline, title, allowance.</summary>
    public string? Sort { get; init; }
}

/// <summary>
/// Public project list row. Deliberately excludes private company documents,
/// internal approval notes, team member data, and payment data (14.01).
/// </summary>
public sealed record PublicProjectListItemResponse(
    Guid Id,
    string Code,
    string Title,
    string Slug,
    string? Summary,
    string Status,
    string Difficulty,
    string WorkType,
    int DurationWeeks,
    DateTimeOffset? ApplicationDeadline,
    int ExpectedStudentCount,
    long? AllowanceAmount,
    string? AllowanceCurrency,
    string CompanyName,
    string CompanySlug,
    IReadOnlyCollection<string> SkillNames);

/// <summary>Public project detail; never exposes internal workflow or private company data.</summary>
public sealed record PublicProjectDetailResponse(
    Guid Id,
    string Code,
    string Title,
    string Slug,
    string? Summary,
    string? ProblemStatement,
    string? BusinessRequirements,
    string? TechnicalConstraints,
    string Status,
    string Difficulty,
    string WorkType,
    int DurationWeeks,
    DateTimeOffset? ApplicationDeadline,
    DateTimeOffset CreatedAt,
    int MinTeamSize,
    int MaxTeamSize,
    int ExpectedStudentCount,
    long? AllowanceAmount,
    string? AllowanceCurrency,
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    Guid? IndustryId,
    string? IndustryName,
    IReadOnlyCollection<PublicProjectSkillResponse> Skills,
    IReadOnlyCollection<PublicProjectDeliverableResponse> Deliverables);

public sealed record PublicProjectSkillResponse(
    Guid SkillId,
    string SkillName,
    string RequirementLevel,
    bool IsRequired);

public sealed record PublicProjectDeliverableResponse(
    string Name,
    string? Description,
    int SortOrder);
