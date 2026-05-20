using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.Process;

public sealed class ProcessCheckPlugin : ICheckPlugin
{
    public string PluginId => PluginIds.Process;
    public void RegisterHealthChecks(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var options = configuration.GetSection("Monitor:Plugins:Process").Get<ProcessPluginOptions>() ?? new();
        if (!PluginRegistration.ShouldRegister(options)) return;
        foreach (var item in options.Items.Where(x => x.Enabled)) builder.Add(PluginRegistration.Registration(PluginId, item, _ => new ProcessHealthCheck(item), options.DefaultInterval));
    }
}
