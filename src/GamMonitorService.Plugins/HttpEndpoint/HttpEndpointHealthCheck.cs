using System.Security.Cryptography.X509Certificates;
using GamMonitorService.Domain.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GamMonitorService.Plugins.HttpEndpoint;

public sealed class HttpEndpointHealthCheck(HttpEndpointCheckItemOptions options, IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri)) return HealthCheckResult.Unhealthy("Url is invalid", data: PluginRegistration.Data(options));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(options.Method), uri);
            foreach (var (key, value) in options.Headers) request.Headers.TryAddWithoutValidation(key, value);
            using var client = CreateClient();
            using var response = await client.SendAsync(request, timeoutCts.Token);
            var statusCode = (int)response.StatusCode;
            return IsAccepted(statusCode)
                ? HealthCheckResult.Healthy($"{options.Name} returned {statusCode} {response.StatusCode}", PluginRegistration.Data(options))
                : HealthCheckResult.Unhealthy($"{options.Name} returned unexpected status {statusCode} {response.StatusCode}", data: PluginRegistration.Data(options));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"{options.Name} request failed: {ex.Message}", ex, PluginRegistration.Data(options));
        }
    }

    private HttpClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(options.ClientCertificatePath))
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = Timeout.InfiniteTimeSpan;
            return client;
        }
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(new X509Certificate2(options.ClientCertificatePath, options.ClientCertificatePassword));
        return new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private bool IsAccepted(int statusCode) => options.AcceptedStatusCodes.Contains(statusCode) || options.AcceptedStatusCodeRanges.Any(range => IsInRange(statusCode, range));
    private static bool IsInRange(int statusCode, string range)
    {
        var parts = range.Split('-', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end) && statusCode >= start && statusCode <= end;
    }
}
