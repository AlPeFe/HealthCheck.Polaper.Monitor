namespace GamMonitorService.Domain;

public sealed record CheckResult(
    string PluginId,
    string ItemId,
    string Name,
    CheckStatus Status,
    string? Message,
    DateTimeOffset CheckedAt,
    TimeSpan Duration);
