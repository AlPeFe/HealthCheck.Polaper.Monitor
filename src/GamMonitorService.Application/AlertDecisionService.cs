using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GamMonitorService.Application;

public sealed class AlertDecisionService(
    ICheckStateStore stateStore,
    IEmailNotificationSender emailSender,
    ISystemClock clock,
    IOptions<MonitorOptions> options,
    ILogger<AlertDecisionService> logger)
{
    public async Task ProcessAsync(CheckResult result, CancellationToken cancellationToken)
    {
        var previous = await stateStore.GetAsync(result.PluginId, result.ItemId, cancellationToken);
        var next = BuildNextState(result, previous);

        await stateStore.UpsertAsync(next, cancellationToken);

        if (ShouldSendRecovery(result, previous))
        {
            await SendAsync(AlertNotificationKind.Recovery, result, next, previous?.LastSuccessAt, cancellationToken);
            return;
        }

        if (ShouldSendFailure(result, next))
        {
            await SendAsync(AlertNotificationKind.Failure, result, next, previous?.LastSuccessAt, cancellationToken);
        }
    }

    private CheckState BuildNextState(CheckResult result, CheckState? previous)
    {
        var isFailure = result.Status is CheckStatus.Error or CheckStatus.Warning;
        var consecutiveFailures = isFailure ? (previous?.ConsecutiveFailures ?? 0) + 1 : 0;
        var now = clock.UtcNow;

        return new CheckState(result.PluginId, result.ItemId, result.Name, result.Status, consecutiveFailures, result.CheckedAt, result.Status == CheckStatus.Ok ? now : previous?.LastSuccessAt, isFailure ? now : previous?.LastFailureAt, previous?.LastAlertSentAt, previous?.LastRecoverySentAt, result.Message);
    }

    private bool ShouldSendRecovery(CheckResult result, CheckState? previous) =>
        result.Status == CheckStatus.Ok && previous is not null && previous.Status is CheckStatus.Warning or CheckStatus.Error;

    private bool ShouldSendFailure(CheckResult result, CheckState state)
    {
        if (result.Status is not (CheckStatus.Error or CheckStatus.Warning)) return false;
        var threshold = options.Value.GetFailureThreshold(result.PluginId, result.ItemId);
        if (state.ConsecutiveFailures < threshold) return false;
        var throttle = options.Value.Alerts.DefaultThrottle;
        return state.LastAlertSentAt is null || clock.UtcNow - state.LastAlertSentAt >= throttle;
    }

    private async Task SendAsync(AlertNotificationKind kind, CheckResult result, CheckState state, DateTimeOffset? lastSuccessAt, CancellationToken cancellationToken)
    {
        var notification = new AlertNotification(kind, options.Value.InstanceId, result, state, lastSuccessAt);
        try
        {
            await emailSender.SendAsync(notification, cancellationToken);
            var updatedState = kind == AlertNotificationKind.Failure ? state with { LastAlertSentAt = clock.UtcNow } : state with { LastRecoverySentAt = clock.UtcNow, LastAlertSentAt = null };
            await stateStore.UpsertAsync(updatedState, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {NotificationKind} notification for {PluginId}:{ItemId}", kind, result.PluginId, result.ItemId);
        }
    }
}
