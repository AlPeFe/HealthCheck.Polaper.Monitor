using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Domain;

public interface ICheckPlugin
{
    string PluginId { get; }
    void RegisterHealthChecks(IHealthChecksBuilder builder, IConfiguration configuration);
}

public interface ICheckStateReader
{
    Task<IReadOnlyCollection<CheckState>> GetAllAsync(CancellationToken cancellationToken);
}

public interface ICheckStateStore : ICheckStateReader
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<CheckState?> GetAsync(string pluginId, string itemId, CancellationToken cancellationToken);
    Task UpsertAsync(CheckState state, CancellationToken cancellationToken);
}

public interface IEmailNotificationSender
{
    Task SendAsync(AlertNotification notification, CancellationToken cancellationToken);
}

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
