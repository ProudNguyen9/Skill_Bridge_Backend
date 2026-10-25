namespace DNTU.SkillBridge.Domain.Identity;

public static class PermissionNames
{
    public const string FilesUpload = "files.upload";
    public const string FilesRead = "files.read";
    public const string FilesDelete = "files.delete";
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsApprove = "projects.approve";
    public const string ProjectsApply = "projects.apply";
    public const string ApplicationsReview = "applications.review";
    public const string TasksManage = "tasks.manage";
    public const string MeetingsManage = "meetings.manage";
    public const string SubmissionsCreate = "submissions.create";
    public const string SubmissionsReviewTechnical = "submissions.review.technical";
    public const string SubmissionsReviewBusiness = "submissions.review.business";
    public const string RubricsManage = "rubrics.manage";
    public const string EvaluationsManage = "evaluations.manage";
    public const string SkillsVerify = "skills.verify";
    public const string PaymentsView = "payments.view";
    public const string PaymentsFund = "payments.fund";
    public const string PaymentsReconcile = "payments.reconcile";
    public const string DisbursementsManage = "disbursements.manage";
    public const string AdminSettingsManage = "admin.settings.manage";
    public const string CompaniesVerify = "companies.verify";
    public const string LecturersApprove = "lecturers.approve";

    /// <summary>Danh sách toàn bộ permission (reflection vì các field là const string).</summary>
    public static IReadOnlyList<string> All =>
    [
        FilesUpload, FilesRead, FilesDelete,
        ProjectsCreate, ProjectsApprove, ProjectsApply,
        ApplicationsReview, TasksManage, MeetingsManage,
        SubmissionsCreate, SubmissionsReviewTechnical, SubmissionsReviewBusiness,
        RubricsManage, EvaluationsManage, SkillsVerify,
        PaymentsView, PaymentsFund, PaymentsReconcile, DisbursementsManage,
        AdminSettingsManage, CompaniesVerify, LecturersApprove,
    ];
}
