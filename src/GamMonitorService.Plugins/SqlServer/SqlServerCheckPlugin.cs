using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.SqlServer;

public sealed class SqlServerCheckPlugin : ICheckPlugin
{
    public string PluginId => PluginIds.SqlServer;
    public void RegisterHealthChecks(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var options = configuration.GetSection("Monitor:Plugins:SqlServer").Get<SqlServerPluginOptions>() ?? new();
        if (!PluginRegistration.ShouldRegister(options)) return;
        foreach (var item in options.Items.Where(x => x.Enabled)) builder.Add(PluginRegistration.Registration(PluginId, item, _ => new SqlServerHealthCheck(item, configuration), options.DefaultInterval));
    }
}
