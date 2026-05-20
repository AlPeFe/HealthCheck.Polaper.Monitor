using System.Text;
using GamMonitorService.Domain;
using GamMonitorService.Domain.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GamMonitorService.Dashboard;

public static class DashboardEndpoints
{
    public static WebApplication MapMonitorDashboard(this WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/dashboard"));
        app.MapGet("/dashboard", RenderDashboardAsync);
        app.MapGet("/api/checks", async (ICheckStateReader reader, CancellationToken cancellationToken) => Results.Ok(await reader.GetAllAsync(cancellationToken)));
        app.MapGet("/api/health", async (HealthCheckService service, CancellationToken cancellationToken) =>
        {
            var report = await service.CheckHealthAsync(_ => true, cancellationToken);
            return Results.Json(new { status = report.Status.ToString(), totalDurationMs = report.TotalDuration.TotalMilliseconds, entries = report.Entries.Select(x => new { name = x.Key, status = x.Value.Status.ToString(), description = x.Value.Description, durationMs = x.Value.Duration.TotalMilliseconds }) });
        });
        return app;
    }

    private static async Task<IResult> RenderDashboardAsync(ICheckStateReader reader, IOptions<MonitorOptions> options, CancellationToken cancellationToken)
    {
        var states = await reader.GetAllAsync(cancellationToken);
        var overall = states.Count == 0 ? CheckStatus.Unknown : states.Any(x => x.Status == CheckStatus.Error) ? CheckStatus.Error : states.Any(x => x.Status == CheckStatus.Warning) ? CheckStatus.Warning : states.All(x => x.Status == CheckStatus.Ok) ? CheckStatus.Ok : CheckStatus.Unknown;
        var html = new StringBuilder();
        html.Append($$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta http-equiv="refresh" content="30"><title>GamMonitorService</title>
            <style>body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:#f5f7fb;color:#172033}header{background:#172033;color:#fff;padding:20px 32px}main{padding:24px 32px}table{width:100%;border-collapse:collapse;background:#fff;border:1px solid #d8deea}th,td{padding:12px 14px;border-bottom:1px solid #e8ecf4;text-align:left;font-size:14px}th{background:#edf1f7;color:#34405a;font-weight:600}.status{display:inline-block;min-width:76px;padding:4px 8px;border-radius:4px;font-weight:700;font-size:12px;text-align:center}.Ok{background:#d8f3dc;color:#176b34}.Warning{background:#fff3bf;color:#7a5600}.Error{background:#ffd7d7;color:#9d1c1c}.Unknown{background:#e5e7eb;color:#4b5563}.summary{display:flex;gap:24px;align-items:center;margin-bottom:20px}.summary strong{font-size:18px}</style></head><body><header><h1>GamMonitorService</h1></header><main><div class="summary"><strong>Instance: {{Html(options.Value.InstanceId)}}</strong><span class="status {{overall}}">{{overall}}</span></div><table><thead><tr><th>Plugin</th><th>Item</th><th>Name</th><th>Status</th><th>Last check UTC</th><th>Message</th></tr></thead><tbody>
            """);
        if (states.Count == 0) html.Append("<tr><td colspan=\"6\">No check results have been published yet.</td></tr>");
        foreach (var state in states)
        {
            html.Append($$"<tr><td>{{Html(state.PluginId)}}</td><td>{{Html(state.ItemId)}}</td><td>{{Html(state.Name)}}</td><td><span class=\"status {{state.Status}}\">{{state.Status}}</span></td><td>{{state.LastCheckedAt?.ToString("O")}}</td><td>{{Html(state.LastMessage ?? "")}}</td></tr>");
        }
        html.Append("</tbody></table></main></body></html>");
        return Results.Content(html.ToString(), "text/html");
    }

    private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value);
}
