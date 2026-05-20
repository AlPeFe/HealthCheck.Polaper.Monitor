using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.Certificate;

public sealed class CertificateCheckPlugin : ICheckPlugin
{
    public string PluginId => PluginIds.Certificate;
    public void RegisterHealthChecks(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var options = configuration.GetSection("Monitor:Plugins:Certificate").Get<CertificatePluginOptions>() ?? new();
        if (!PluginRegistration.ShouldRegister(options)) return;
        foreach (var item in options.Items.Where(x => x.Enabled)) builder.Add(PluginRegistration.Registration(PluginId, item, _ => new CertificateHealthCheck(item), options.DefaultInterval));
    }
}
