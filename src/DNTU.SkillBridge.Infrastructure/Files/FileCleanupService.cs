using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Application.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Infrastructure.Files;

/// <summary>Configurable worker integration point for expiry cleanup until the Quartz scheduler is introduced.</summary>
public sealed class FileCleanupService(IServiceScopeFactory scopeFactory, IOptions<QuartzOptions> quartzOptions, ILogger<FileCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!quartzOptions.Value.Enabled) return;
        var interval = TimeSpan.FromMinutes(quartzOptions.Value.FileCleanupIntervalMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var removed = await scope.ServiceProvider.GetRequiredService<IFileExpirationService>().ExpirePendingUploadsAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (removed > 0) logger.LogInformation("Expired {ExpiredFileCount} pending file upload(s).", removed);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Pending file upload cleanup cycle failed.");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }
}
