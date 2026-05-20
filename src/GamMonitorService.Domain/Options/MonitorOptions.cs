namespace GamMonitorService.Domain.Options;

public sealed class MonitorOptions
{
    public const string SectionName = "Monitor";
    public string InstanceId { get; init; } = Environment.MachineName;
    public AlertOptions Alerts { get; init; } = new();
    public DashboardOptions Dashboard { get; init; } = new();
    public MonitorHealthChecksOptions HealthChecks { get; init; } = new();
    public PersistenceOptions Persistence { get; init; } = new();
    public PluginOptions Plugins { get; init; } = new();

    public int GetFailureThreshold(string pluginId, string itemId)
    {
        var item = Plugins.FindItem(pluginId, itemId);
        return Math.Max(1, item?.FailureThreshold ?? 1);
    }
}

public sealed class AlertOptions
{
    public TimeSpan DefaultThrottle { get; init; } = TimeSpan.FromMinutes(30);
    public EmailOptions Email { get; init; } = new();
}

public sealed class EmailOptions
{
    public bool Enabled { get; init; }
    public string From { get; init; } = "";
    public IReadOnlyCollection<string> To { get; init; } = [];
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public string Security { get; init; } = "StartTls";
    public string? Username { get; init; }
    public string? Password { get; init; }
}

public sealed class DashboardOptions
{
    public bool Enabled { get; init; } = true;
    public IReadOnlyCollection<string> Urls { get; init; } = [];
}

public sealed class MonitorHealthChecksOptions
{
    public bool Enabled { get; init; } = true;
    public PublisherOptions Publisher { get; init; } = new();
    public HealthEndpointOptions Endpoints { get; init; } = new();
}

public sealed class PublisherOptions
{
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan Period { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public string RequiredTag { get; init; } = "monitor";
}

public sealed class HealthEndpointOptions
{
    public string All { get; init; } = "/healthz";
    public string Live { get; init; } = "/healthz/live";
    public string Ready { get; init; } = "/healthz/ready";
}

public sealed class PersistenceOptions
{
    public string Provider { get; init; } = "Sqlite";
    public string ConnectionString { get; init; } = "Data Source=gam-monitor-state.db";
    public bool EnableHistory { get; init; }
    public int HistoryRetentionDays { get; init; } = 7;
}
