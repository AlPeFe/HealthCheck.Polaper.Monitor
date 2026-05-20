using GamMonitorService.Domain;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace GamMonitorService.Application;

public sealed class MonitorHealthCheckPublisher(
    AlertDecisionService alertDecisionService,
    ISystemClock clock,
    ILogger<MonitorHealthCheckPublisher> logger) : IHealthCheckPublisher
{
    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var (registrationName, entry) in report.Entries)
        {
            var result = Normalize(registrationName, entry);
            logger.LogInformation("Health check {PluginId}:{ItemId} completed with {CheckStatus} in {DurationMs} ms", result.PluginId, result.ItemId, result.Status, result.Duration.TotalMilliseconds);
            await alertDecisionService.ProcessAsync(result, cancellationToken);
        }
    }

    private CheckResult Normalize(string registrationName, HealthReportEntry entry)
    {
        var (pluginId, itemId) = ParseName(registrationName);
        var name = entry.Data.TryGetValue("name", out var displayName) ? displayName?.ToString() ?? registrationName : registrationName;
        var message = entry.Description;
        if (string.IsNullOrWhiteSpace(message) && entry.Exception is not null) message = entry.Exception.Message;
        return new CheckResult(pluginId, itemId, name, MapStatus(entry.Status), message, clock.UtcNow, entry.Duration);
    }

    private static (string PluginId, string ItemId) ParseName(string registrationName)
    {
        var parts = registrationName.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("unknown", registrationName);
    }

    private static CheckStatus MapStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => CheckStatus.Ok,
        HealthStatus.Degraded => CheckStatus.Warning,
        HealthStatus.Unhealthy => CheckStatus.Error,
        _ => CheckStatus.Unknown
    };
}
