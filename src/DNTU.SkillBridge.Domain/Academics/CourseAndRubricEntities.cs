using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Academics;

public sealed class Course : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Course() { }
    public Course(string code, string name)
    {
        Code = Required(code, 32);
        Name = Required(name, 300);
    }

    public void Update(string code, string name)
    {
        Code = Required(code, 32);
        Name = Required(name, 300);
    }

    private static string Required(string value, int maxLength) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException("A valid value is required.", nameof(value));
}

public sealed class CourseProject : AuditableEntity
{
    public Guid CourseId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid RubricId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public CourseProjectStatus Status { get; private set; } = CourseProjectStatus.DRAFT;
    public DateTimeOffset? ApprovedAt { get; private set; }

    private CourseProject() { }

    public CourseProject(Guid courseId, Guid projectId, Guid rubricId, Guid createdByUserId)
    {
        if (courseId == Guid.Empty || projectId == Guid.Empty || rubricId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Course-project mapping identifiers are required.");
        }

        CourseId = courseId;
        ProjectId = projectId;
        RubricId = rubricId;
        CreatedByUserId = createdByUserId;
    }

    public void Update(Guid courseId, Guid rubricId)
    {
        if (Status != CourseProjectStatus.DRAFT)
        {
            throw new InvalidOperationException("Only draft course-project mappings can be updated.");
        }

        CourseId = courseId == Guid.Empty ? throw new ArgumentException("A course is required.", nameof(courseId)) : courseId;
        RubricId = rubricId == Guid.Empty ? throw new ArgumentException("A rubric is required.", nameof(rubricId)) : rubricId;
    }

    public void Approve(Guid approvedByUserId, DateTimeOffset at)
    {
        if (Status != CourseProjectStatus.DRAFT)
        {
            throw new InvalidOperationException("Only draft course-project mappings can be approved.");
        }

        Status = CourseProjectStatus.APPROVED;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = at;
    }

    public void Remove()
    {
        if (Status == CourseProjectStatus.REMOVED)
        {
            return;
        }

        Status = CourseProjectStatus.REMOVED;
    }
}

public sealed class Rubric : AuditableEntity
{
    public Guid LecturerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsLocked { get; private set; }
    public Guid Version { get; private set; } = Guid.CreateVersion7();
    public ICollection<RubricCriterion> Criteria { get; } = [];

    private Rubric() { }
    public Rubric(Guid lecturerId, string name)
    {
        LecturerId = lecturerId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A rubric name is required.", nameof(name)) : name.Trim();
    }

    public void AddCriterion(string name, decimal weight, int sortOrder)
    {
        if (IsLocked || weight <= 0 || weight > 100 || Criteria.Any(item => item.SortOrder == sortOrder)) throw new InvalidOperationException("Invalid rubric criterion.");
        Criteria.Add(new RubricCriterion(Id, name, weight, sortOrder));
    }

    public void UpdateCriterion(Guid criterionId, string name, decimal weight, int sortOrder)
    {
        if (IsLocked || weight <= 0 || weight > 100 || Criteria.Any(item => item.Id != criterionId && item.SortOrder == sortOrder))
        {
            throw new InvalidOperationException("Invalid rubric criterion.");
        }

        var criterion = Criteria.SingleOrDefault(item => item.Id == criterionId)
            ?? throw new InvalidOperationException("Rubric criterion was not found.");
        criterion.Update(name, weight, sortOrder);
    }

    public void RemoveCriterion(Guid criterionId)
    {
        if (IsLocked)
        {
            throw new InvalidOperationException("A locked rubric cannot be changed.");
        }

        var criterion = Criteria.SingleOrDefault(item => item.Id == criterionId)
            ?? throw new InvalidOperationException("Rubric criterion was not found.");
        Criteria.Remove(criterion);
    }

    public bool HasCompleteWeights() => Criteria.Sum(item => item.Weight) == 100m;
    public void Lock()
    {
        if (!HasCompleteWeights()) throw new InvalidOperationException("Rubric criterion weights must total 100.");
        IsLocked = true;
        Version = Guid.CreateVersion7();
    }
}

public sealed class RubricCriterion : AuditableEntity
{
    public Guid RubricId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Weight { get; private set; }
    public int SortOrder { get; private set; }

    private RubricCriterion() { }
    public RubricCriterion(Guid rubricId, string name, decimal weight, int sortOrder)
    {
        RubricId = rubricId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A criterion name is required.", nameof(name)) : name.Trim();
        Weight = weight;
        SortOrder = sortOrder;
    }

    public void Update(string name, decimal weight, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 300 || weight <= 0 || weight > 100 || sortOrder < 1)
        {
            throw new ArgumentException("Invalid rubric criterion.");
        }

        Name = name.Trim();
        Weight = weight;
        SortOrder = sortOrder;
    }
}
