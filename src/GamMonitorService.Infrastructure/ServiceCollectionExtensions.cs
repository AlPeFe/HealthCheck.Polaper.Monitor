using GamMonitorService.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace GamMonitorService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMonitorInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ICheckStateStore, SqliteCheckStateStore>();
        services.AddSingleton<ICheckStateReader>(sp => sp.GetRequiredService<ICheckStateStore>());
        services.AddSingleton<IEmailNotificationSender, MailKitEmailNotificationSender>();
        services.AddHostedService<DatabaseInitializerHostedService>();
        return services;
    }
}
