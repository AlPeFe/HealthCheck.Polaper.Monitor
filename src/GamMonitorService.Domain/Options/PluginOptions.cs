namespace GamMonitorService.Domain.Options;

public sealed class PluginOptions
{
    public ProcessPluginOptions Process { get; init; } = new();
    public HttpEndpointPluginOptions HttpEndpoint { get; init; } = new();
    public DiskSpacePluginOptions DiskSpace { get; init; } = new();
    public SqlServerPluginOptions SqlServer { get; init; } = new();
    public CertificatePluginOptions Certificate { get; init; } = new();

    public CheckItemOptions? FindItem(string pluginId, string itemId)
    {
        IEnumerable<CheckItemOptions> items = pluginId switch
        {
            PluginIds.Process => Process.Items,
            PluginIds.HttpEndpoint => HttpEndpoint.Items,
            PluginIds.DiskSpace => DiskSpace.Items,
            PluginIds.SqlServer => SqlServer.Items,
            PluginIds.Certificate => Certificate.Items,
            _ => []
        };

        return items.FirstOrDefault(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase));
    }
}

public static class PluginIds
{
    public const string Process = "process";
    public const string HttpEndpoint = "http-endpoint";
    public const string DiskSpace = "disk-space";
    public const string SqlServer = "sql-server";
    public const string Certificate = "certificate";
}

public abstract class PluginOptionsBase<TItem> where TItem : CheckItemOptions
{
    public string PluginId { get; init; } = "";
    public bool Enabled { get; init; }
    public TimeSpan DefaultInterval { get; init; } = TimeSpan.FromMinutes(5);
    public IReadOnlyCollection<TItem> Items { get; init; } = [];
}

public abstract class CheckItemOptions
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public int FailureThreshold { get; init; } = 1;
}

public sealed class ProcessPluginOptions : PluginOptionsBase<ProcessCheckItemOptions>
{
    public ProcessPluginOptions() => PluginId = PluginIds.Process;
}

public sealed class ProcessCheckItemOptions : CheckItemOptions
{
    public string ProcessName { get; init; } = "";
    public string? ExecutablePath { get; init; }
    public bool RestartIfMissing { get; init; }
}

public sealed class HttpEndpointPluginOptions : PluginOptionsBase<HttpEndpointCheckItemOptions>
{
    public HttpEndpointPluginOptions() => PluginId = PluginIds.HttpEndpoint;
}

public sealed class HttpEndpointCheckItemOptions : CheckItemOptions
{
    public string Url { get; init; } = "";
    public string Method { get; init; } = "GET";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public string? ClientCertificatePath { get; init; }
    public string? ClientCertificatePassword { get; init; }
    public IReadOnlyCollection<int> AcceptedStatusCodes { get; init; } = [200];
    public IReadOnlyCollection<string> AcceptedStatusCodeRanges { get; init; } = [];
}

public sealed class DiskSpacePluginOptions : PluginOptionsBase<DiskSpaceCheckItemOptions>
{
    public DiskSpacePluginOptions() => PluginId = PluginIds.DiskSpace;
}

public sealed class DiskSpaceCheckItemOptions : CheckItemOptions
{
    public string Path { get; init; } = "";
    public double MinimumFreeGb { get; init; }
    public double? MinimumFreePercent { get; init; }
}

public sealed class SqlServerPluginOptions : PluginOptionsBase<SqlServerCheckItemOptions>
{
    public SqlServerPluginOptions() => PluginId = PluginIds.SqlServer;
}

public sealed class SqlServerCheckItemOptions : CheckItemOptions
{
    public string? ConnectionString { get; init; }
    public string? ConnectionStringName { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public string Query { get; init; } = "SELECT 1";
    public string ExpectedScalarResult { get; init; } = "1";
}

public sealed class CertificatePluginOptions : PluginOptionsBase<CertificateCheckItemOptions>
{
    public CertificatePluginOptions() => PluginId = PluginIds.Certificate;
}

public sealed class CertificateCheckItemOptions : CheckItemOptions
{
    public string Path { get; init; } = "";
    public string? Password { get; init; }
    public int ExpirationWarningDays { get; init; } = 30;
}
