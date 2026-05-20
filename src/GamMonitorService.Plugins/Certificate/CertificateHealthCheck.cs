using System.Security.Cryptography.X509Certificates;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.Certificate;

public sealed class CertificateHealthCheck(CertificateCheckItemOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(options.Path)) return Task.FromResult(HealthCheckResult.Unhealthy("Certificate file does not exist", data: PluginRegistration.Data(options)));
            using var certificate = new X509Certificate2(options.Path, options.Password);
            var expiresAt = new DateTimeOffset(certificate.NotAfter);
            var daysRemaining = (expiresAt - DateTimeOffset.UtcNow).TotalDays;
            var data = PluginRegistration.Data(options);
            data["expiresAt"] = expiresAt.ToString("O");
            data["daysRemaining"] = Math.Round(daysRemaining, 1);
            if (daysRemaining < 0) return Task.FromResult(HealthCheckResult.Unhealthy($"{options.Name} certificate is expired", data: data));
            if (daysRemaining <= options.ExpirationWarningDays) return Task.FromResult(HealthCheckResult.Degraded($"{options.Name} certificate expires in {daysRemaining:F1} days", data: data));
            return Task.FromResult(HealthCheckResult.Healthy($"{options.Name} certificate expires in {daysRemaining:F1} days", data));
        }
        catch (Exception ex) { return Task.FromResult(HealthCheckResult.Unhealthy($"{options.Name} certificate check failed: {ex.Message}", ex, PluginRegistration.Data(options))); }
    }
}
