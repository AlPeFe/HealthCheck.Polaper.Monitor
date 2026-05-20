using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.DiskSpace;

public sealed class DiskSpaceCheckPlugin : ICheckPlugin
{
    public string PluginId => PluginIds.DiskSpace;
    public void RegisterHealthChecks(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var options = configuration.GetSection("Monitor:Plugins:DiskSpace").Get<DiskSpacePluginOptions>() ?? new();
        if (!PluginRegistration.ShouldRegister(options)) return;
        foreach (var item in options.Items.Where(x => x.Enabled)) builder.Add(PluginRegistration.Registration(PluginId, item, _ => new DiskSpaceHealthCheck(item), options.DefaultInterval));
    }
}
