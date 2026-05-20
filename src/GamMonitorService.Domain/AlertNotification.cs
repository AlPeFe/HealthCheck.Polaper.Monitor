namespace GamMonitorService.Domain;

public enum AlertNotificationKind
{
    Failure,
    Recovery
}

public sealed record AlertNotification(
    AlertNotificationKind Kind,
    string InstanceId,
    CheckResult Result,
    CheckState State,
    DateTimeOffset? LastSuccessAt);
