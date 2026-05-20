using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GamMonitorService.Infrastructure;

public sealed class MailKitEmailNotificationSender(IOptions<MonitorOptions> options, ILogger<MailKitEmailNotificationSender> logger) : IEmailNotificationSender
{
    public async Task SendAsync(AlertNotification notification, CancellationToken cancellationToken)
    {
        var emailOptions = options.Value.Alerts.Email;
        if (!emailOptions.Enabled)
        {
            logger.LogInformation("Email alerting is disabled. Suppressed {Kind} notification for {PluginId}:{ItemId}", notification.Kind, notification.Result.PluginId, notification.Result.ItemId);
            return;
        }

        if (string.IsNullOrWhiteSpace(emailOptions.From) || string.IsNullOrWhiteSpace(emailOptions.SmtpHost) || emailOptions.To.Count == 0)
        {
            logger.LogWarning("Email alerting is enabled but SMTP settings are incomplete");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(emailOptions.From));
        foreach (var recipient in emailOptions.To) message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = BuildSubject(notification);
        message.Body = new TextPart("plain") { Text = BuildBody(notification) };

        using var client = new SmtpClient();
        await client.ConnectAsync(emailOptions.SmtpHost, emailOptions.SmtpPort, ParseSocketOptions(emailOptions.Security), cancellationToken);
        if (!string.IsNullOrWhiteSpace(emailOptions.Username)) await client.AuthenticateAsync(emailOptions.Username, emailOptions.Password ?? "", cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static string BuildSubject(AlertNotification notification)
    {
        var prefix = notification.Kind == AlertNotificationKind.Recovery ? "RECOVERY" : "ALERT";
        return $"[{prefix}] {notification.InstanceId} {notification.Result.PluginId}:{notification.Result.ItemId} {notification.Result.Status}";
    }

    private static string BuildBody(AlertNotification notification) => $"""
        Instance: {notification.InstanceId}
        Plugin: {notification.Result.PluginId}
        Item: {notification.Result.ItemId}
        Name: {notification.Result.Name}
        Status: {notification.Result.Status}
        Reason: {notification.Result.Message}
        Timestamp UTC: {notification.Result.CheckedAt:O}
        Last success UTC: {notification.LastSuccessAt:O}
        Consecutive failures: {notification.State.ConsecutiveFailures}
        """;

    private static SecureSocketOptions ParseSocketOptions(string value) =>
        value.Equals("StartTls", StringComparison.OrdinalIgnoreCase) ? SecureSocketOptions.StartTls :
        value.Equals("SslOnConnect", StringComparison.OrdinalIgnoreCase) ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
}
