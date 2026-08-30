using DNTU.SkillBridge.Domain.Academics;
using DNTU.SkillBridge.Domain.Administration;
using DNTU.SkillBridge.Domain.Analytics;
using DNTU.SkillBridge.Domain.Catalog;
using DNTU.SkillBridge.Domain.Common;
using DNTU.SkillBridge.Domain.Companies;
using DNTU.SkillBridge.Domain.Commitments;
using DNTU.SkillBridge.Domain.Identity;
using DNTU.SkillBridge.Domain.Files;
using DNTU.SkillBridge.Domain.Lecturers;
using DNTU.SkillBridge.Domain.Notifications;
using DNTU.SkillBridge.Domain.Projects;
using DNTU.SkillBridge.Domain.Milestones;
using DNTU.SkillBridge.Domain.Meetings;
using DNTU.SkillBridge.Domain.Students;
using DNTU.SkillBridge.Domain.Submissions;
using DNTU.SkillBridge.Domain.Portfolio;
using DNTU.SkillBridge.Domain.Payments;
using DNTU.SkillBridge.Domain.Teams;
using DNTU.SkillBridge.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace DNTU.SkillBridge.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Industry> Industries => Set<Industry>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Major> Majors => Set<Major>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<StudentPrivacySettings> StudentPrivacySettings => Set<StudentPrivacySettings>();
    public DbSet<StudentSkill> StudentSkills => Set<StudentSkill>();
    public DbSet<StudentCertificate> StudentCertificates => Set<StudentCertificate>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
    public DbSet<CompanyInvitation> CompanyInvitations => Set<CompanyInvitation>();
    public DbSet<CompanyDocument> CompanyDocuments => Set<CompanyDocument>();
    public DbSet<LecturerProfile> LecturerProfiles => Set<LecturerProfile>();
    public DbSet<LecturerAssignment> LecturerAssignments => Set<LecturerAssignment>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectSkill> ProjectSkills => Set<ProjectSkill>();
    public DbSet<ProjectDeliverable> ProjectDeliverables => Set<ProjectDeliverable>();
    public DbSet<ProjectApproval> ProjectApprovals => Set<ProjectApproval>();
    public DbSet<ProjectCompletion> ProjectCompletions => Set<ProjectCompletion>();
    public DbSet<SavedProject> SavedProjects => Set<SavedProject>();
    public DbSet<DNTU.SkillBridge.Domain.Applications.Application> Applications => Set<DNTU.SkillBridge.Domain.Applications.Application>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<ProjectCommitment> ProjectCommitments => Set<ProjectCommitment>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<WorkspaceSettings> WorkspaceSettings => Set<WorkspaceSettings>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectActivity> ProjectActivities => Set<ProjectActivity>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<FileRecord> FileRecords => Set<FileRecord>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<MilestoneDeliverable> MilestoneDeliverables => Set<MilestoneDeliverable>();
    public DbSet<MilestoneApprovalHistory> MilestoneApprovalHistories => Set<MilestoneApprovalHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProjectMeeting> ProjectMeetings => Set<ProjectMeeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingMinute> MeetingMinutes => Set<MeetingMinute>();
    public DbSet<MeetingMinuteRevision> MeetingMinuteRevisions => Set<MeetingMinuteRevision>();
    public DbSet<MeetingActionItem> MeetingActionItems => Set<MeetingActionItem>();
    public DbSet<ProjectSubmission> ProjectSubmissions => Set<ProjectSubmission>();
    public DbSet<SubmissionVersion> SubmissionVersions => Set<SubmissionVersion>();
    public DbSet<SubmissionStatusHistory> SubmissionStatusHistories => Set<SubmissionStatusHistory>();
    public DbSet<ProjectRiskSnapshot> ProjectRiskSnapshots => Set<ProjectRiskSnapshot>();
    public DbSet<ProjectRiskHistory> ProjectRiskHistory => Set<ProjectRiskHistory>();
    public DbSet<TechnicalSubmissionReview> TechnicalSubmissionReviews => Set<TechnicalSubmissionReview>();
    public DbSet<BusinessSubmissionReview> BusinessSubmissionReviews => Set<BusinessSubmissionReview>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseProject> CourseProjects => Set<CourseProject>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<AcademicEvaluation> AcademicEvaluations => Set<AcademicEvaluation>();
    public DbSet<AcademicCriterionScore> AcademicCriterionScores => Set<AcademicCriterionScore>();
    public DbSet<VerifiedSkill> VerifiedSkills => Set<VerifiedSkill>();
    public DbSet<PortfolioEntry> PortfolioEntries => Set<PortfolioEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AdminPolicySetting> AdminPolicySettings => Set<AdminPolicySetting>();
    public DbSet<ReportExport> ReportExports => Set<ReportExport>();
    public DbSet<ProjectFunding> ProjectFundings => Set<ProjectFunding>();
    public DbSet<FundingOrder> FundingOrders => Set<FundingOrder>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<MilestoneAllowance> MilestoneAllowances => Set<MilestoneAllowance>();
    public DbSet<AllowanceAllocation> AllowanceAllocations => Set<AllowanceAllocation>();
    public DbSet<StudentPayoutAccount> StudentPayoutAccounts => Set<StudentPayoutAccount>();
    public DbSet<Disbursement> Disbursements => Set<Disbursement>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<SePayIpnEvent> SePayIpnEvents => Set<SePayIpnEvent>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Npgsql timestamptz only accepts UTC offsets; clients may send local offsets (+07:00).
        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, DateTimeOffset>(
            value => value.ToUniversalTime(),
            value => value);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(utcConverter);
                }
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditableEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAuditableEntities()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
