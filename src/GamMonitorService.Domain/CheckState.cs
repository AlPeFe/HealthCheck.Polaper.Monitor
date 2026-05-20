namespace GamMonitorService.Domain;

public sealed record CheckState(
    string PluginId,
    string ItemId,
    string Name,
    CheckStatus Status,
    int ConsecutiveFailures,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    DateTimeOffset? LastAlertSentAt,
    DateTimeOffset? LastRecoverySentAt,
    string? LastMessage);
