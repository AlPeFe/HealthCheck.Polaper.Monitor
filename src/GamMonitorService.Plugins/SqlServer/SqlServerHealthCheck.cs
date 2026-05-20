using GamMonitorService.Domain.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.SqlServer;

public sealed class SqlServerHealthCheck(SqlServerCheckItemOptions options, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return HealthCheckResult.Unhealthy("Connection string is required", data: PluginRegistration.Data(options));
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = (int)Math.Ceiling(options.Timeout.TotalSeconds);
            command.CommandText = string.IsNullOrWhiteSpace(options.Query) ? "SELECT 1" : options.Query;
            var actual = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            return string.Equals(actual, options.ExpectedScalarResult, StringComparison.OrdinalIgnoreCase)
                ? HealthCheckResult.Healthy($"{options.Name} returned expected result", PluginRegistration.Data(options))
                : HealthCheckResult.Unhealthy($"{options.Name} returned '{actual}', expected '{options.ExpectedScalarResult}'", data: PluginRegistration.Data(options));
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy($"{options.Name} SQL check failed: {ex.Message}", ex, PluginRegistration.Data(options)); }
    }

    private string? ResolveConnectionString() => !string.IsNullOrWhiteSpace(options.ConnectionString) ? options.ConnectionString : string.IsNullOrWhiteSpace(options.ConnectionStringName) ? null : configuration.GetConnectionString(options.ConnectionStringName);
}
