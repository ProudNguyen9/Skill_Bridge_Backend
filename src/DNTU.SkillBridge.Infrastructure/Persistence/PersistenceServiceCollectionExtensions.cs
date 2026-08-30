using DNTU.SkillBridge.Application.Administration;
using DNTU.SkillBridge.Application.Analytics;
using DNTU.SkillBridge.Application.Catalog;
using DNTU.SkillBridge.Application.Notifications;
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

        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IAdministrationRepository, AdminGovernanceRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        return services;
    }
}
