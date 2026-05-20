using System.Diagnostics;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.Process;

public sealed class ProcessHealthCheck(ProcessCheckItemOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ProcessName)) return Task.FromResult(HealthCheckResult.Unhealthy("ProcessName is required", data: PluginRegistration.Data(options)));
        var processName = Path.GetFileNameWithoutExtension(options.ProcessName);
        var matching = System.Diagnostics.Process.GetProcessesByName(processName).Any(MatchesExecutablePath);
        if (matching) return Task.FromResult(HealthCheckResult.Healthy($"{options.Name} process is running", PluginRegistration.Data(options)));
        if (options.RestartIfMissing && !string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo(options.ExecutablePath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath) ?? Environment.CurrentDirectory });
                return Task.FromResult(HealthCheckResult.Degraded($"{options.Name} was missing and restart was attempted", data: PluginRegistration.Data(options)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"{options.Name} is missing and restart failed: {ex.Message}", ex, PluginRegistration.Data(options)));
            }
        }
        return Task.FromResult(HealthCheckResult.Unhealthy($"{options.Name} process is missing", data: PluginRegistration.Data(options)));
    }

    private bool MatchesExecutablePath(System.Diagnostics.Process process)
    {
        if (string.IsNullOrWhiteSpace(options.ExecutablePath)) return true;
        try { return string.Equals(process.MainModule?.FileName, options.ExecutablePath, StringComparison.OrdinalIgnoreCase); }
        catch { return true; }
    }
}
