using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.DiskSpace;

public sealed class DiskSpaceHealthCheck(DiskSpaceCheckItemOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = Path.GetPathRoot(options.Path);
            if (string.IsNullOrWhiteSpace(root)) return Task.FromResult(HealthCheckResult.Unhealthy("Path does not resolve to a drive root", data: PluginRegistration.Data(options)));
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return Task.FromResult(HealthCheckResult.Unhealthy($"{root} is not ready", data: PluginRegistration.Data(options)));
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            var freePercent = drive.TotalSize == 0 ? 0 : drive.AvailableFreeSpace * 100d / drive.TotalSize;
            var data = PluginRegistration.Data(options);
            data["freeGb"] = Math.Round(freeGb, 2);
            data["freePercent"] = Math.Round(freePercent, 2);
            var failed = freeGb < options.MinimumFreeGb || (options.MinimumFreePercent is not null && freePercent < options.MinimumFreePercent);
            var message = $"{root} has {freeGb:F2} GB free ({freePercent:F2}%)";
            return Task.FromResult(failed ? HealthCheckResult.Unhealthy(message, data: data) : HealthCheckResult.Healthy(message, data));
        }
        catch (Exception ex) { return Task.FromResult(HealthCheckResult.Unhealthy($"{options.Name} disk check failed: {ex.Message}", ex, PluginRegistration.Data(options))); }
    }
}
