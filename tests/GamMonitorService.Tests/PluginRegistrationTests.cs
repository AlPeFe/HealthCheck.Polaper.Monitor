using GamMonitorService.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GamMonitorService.Tests;

public sealed class PluginRegistrationTests
{
    [Fact]
    public void DisabledPluginsAndEmptyItemListsDoNotRegisterHealthChecks()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Monitor:Plugins:Process:Enabled"] = "false",
            ["Monitor:Plugins:Process:Items:0:Id"] = "gam-console",
            ["Monitor:Plugins:Process:Items:0:Name"] = "GAM Console",
            ["Monitor:Plugins:Process:Items:0:Enabled"] = "true",
            ["Monitor:Plugins:Process:Items:0:ProcessName"] = "gam",
            ["Monitor:Plugins:HttpEndpoint:Enabled"] = "true"
        }).Build();
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddConfiguredMonitorHealthChecks(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Empty(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
    }

    [Fact]
    public void EnabledItemsAreRegisteredWithStableNameAndTags()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Monitor:Plugins:HttpEndpoint:Enabled"] = "true",
            ["Monitor:Plugins:HttpEndpoint:DefaultInterval"] = "00:01:00",
            ["Monitor:Plugins:HttpEndpoint:Items:0:Id"] = "main-api",
            ["Monitor:Plugins:HttpEndpoint:Items:0:Name"] = "Main API",
            ["Monitor:Plugins:HttpEndpoint:Items:0:Enabled"] = "true",
            ["Monitor:Plugins:HttpEndpoint:Items:0:Url"] = "https://example.com",
            ["Monitor:Plugins:HttpEndpoint:Items:0:AcceptedStatusCodes:0"] = "200"
        }).Build();
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddConfiguredMonitorHealthChecks(configuration);
        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal("http-endpoint:main-api", registration.Name);
        Assert.Contains("monitor", registration.Tags);
        Assert.Contains("http-endpoint", registration.Tags);
        Assert.Contains("main-api", registration.Tags);
    }
}
