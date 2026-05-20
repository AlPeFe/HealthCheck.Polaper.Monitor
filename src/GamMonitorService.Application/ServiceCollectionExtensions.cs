using Microsoft.Extensions.DependencyInjection;

namespace GamMonitorService.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMonitorApplication(this IServiceCollection services)
    {
        services.AddSingleton<AlertDecisionService>();
        services.AddSingleton<MonitorHealthCheckPublisher>();
        services.AddSingleton<Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheckPublisher>(sp => sp.GetRequiredService<MonitorHealthCheckPublisher>());
        return services;
    }
}
