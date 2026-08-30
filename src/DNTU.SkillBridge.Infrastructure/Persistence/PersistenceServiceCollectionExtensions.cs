using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Application.Academics;
using DNTU.SkillBridge.Application.Analytics;
using DNTU.SkillBridge.Application.Applications;
using DNTU.SkillBridge.Application.Authentication;
using DNTU.SkillBridge.Application.Catalog;
using DNTU.SkillBridge.Application.Companies;
using DNTU.SkillBridge.Application.Commitments;
using DNTU.SkillBridge.Application.Files;
using DNTU.SkillBridge.Application.Lecturers;
using DNTU.SkillBridge.Application.Milestones;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Payments;
using DNTU.SkillBridge.Application.SePay;
using DNTU.SkillBridge.Application.Students;
using DNTU.SkillBridge.Application.Submissions;
using DNTU.SkillBridge.Application.Teams;
using DNTU.SkillBridge.Application.Workspaces;
using DNTU.SkillBridge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DNTU.SkillBridge.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IAdministrationRepository, AdminGovernanceRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ISePayRepository, SePayRepository>();
        services.AddScoped<ICommitmentRepository, CommitmentRepository>();
        services.AddScoped<IProjectAccessRepository, ProjectAccessRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();
        services.AddScoped<ITaskCollaborationRepository, TaskCollaborationRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ILecturerRepository, LecturerRepository>();
        services.AddScoped<IAcademicRepository, AcademicRepository>();
        services.AddScoped<IAcademicEvaluationRepository, AcademicEvaluationRepository>();
        services.AddScoped<IMilestoneRepository, MilestoneRepository>();
        services.AddScoped<ISavedProjectRepository, SavedProjectRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ITechnicalReviewRepository, TechnicalReviewRepository>();
        services.AddScoped<IBusinessReviewRepository, BusinessReviewRepository>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();

        return services;
    }
}
