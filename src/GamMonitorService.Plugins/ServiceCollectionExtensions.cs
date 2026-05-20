using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using GamMonitorService.Plugins.Certificate;
using GamMonitorService.Plugins.DiskSpace;
using GamMonitorService.Plugins.HttpEndpoint;
using GamMonitorService.Plugins.Process;
using GamMonitorService.Plugins.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBuiltInCheckPlugins(this IServiceCollection services)
    {
        services.AddSingleton<ICheckPlugin, ProcessCheckPlugin>();
        services.AddSingleton<ICheckPlugin, HttpEndpointCheckPlugin>();
        services.AddSingleton<ICheckPlugin, DiskSpaceCheckPlugin>();
        services.AddSingleton<ICheckPlugin, SqlServerCheckPlugin>();
        services.AddSingleton<ICheckPlugin, CertificateCheckPlugin>();
        services.AddHttpClient();
        return services;
    }

    public static IHealthChecksBuilder AddConfiguredMonitorHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();
        ICheckPlugin[] plugins = [new ProcessCheckPlugin(), new HttpEndpointCheckPlugin(), new DiskSpaceCheckPlugin(), new SqlServerCheckPlugin(), new CertificateCheckPlugin()];
        foreach (var plugin in plugins) plugin.RegisterHealthChecks(builder, configuration);
        return builder;
    }
}

internal static class PluginRegistration
{
    public static string Name(string pluginId, string itemId) => $"{pluginId}:{itemId}";
    public static string[] Tags(string pluginId, string itemId) => ["monitor", pluginId, itemId];
    public static bool ShouldRegister<TItem>(PluginOptionsBase<TItem> options) where TItem : CheckItemOptions => options.Enabled && options.Items.Any(x => x.Enabled);
    public static HealthCheckRegistration Registration(string pluginId, CheckItemOptions item, Func<IServiceProvider, IHealthCheck> factory, TimeSpan? period = null)
    {
        var registration = new HealthCheckRegistration(Name(pluginId, item.Id), factory, HealthStatus.Unhealthy, Tags(pluginId, item.Id));
        if (period is not null) registration.Period = period;
        return registration;
    }
    public static Dictionary<string, object> Data(CheckItemOptions item) => new() { ["name"] = item.Name };
}
