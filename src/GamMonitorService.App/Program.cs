using System.Text.Json;
using GamMonitorService.Application;
using GamMonitorService.Dashboard;
using GamMonitorService.Domain.Options;
using GamMonitorService.Infrastructure;
using GamMonitorService.Plugins;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services
    .AddOptions<MonitorOptions>()
    .Bind(builder.Configuration.GetSection(MonitorOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.InstanceId), "Monitor:InstanceId is required")
    .ValidateOnStart();

builder.Services.AddMonitorApplication();
builder.Services.AddMonitorInfrastructure();
builder.Services.AddBuiltInCheckPlugins();
builder.Services.AddConfiguredMonitorHealthChecks(builder.Configuration);

var monitorOptions = builder.Configuration
    .GetSection(MonitorOptions.SectionName)
    .Get<MonitorOptions>() ?? new MonitorOptions();

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = monitorOptions.HealthChecks.Publisher.Delay;
    options.Period = monitorOptions.HealthChecks.Publisher.Period;
    options.Timeout = monitorOptions.HealthChecks.Publisher.Timeout;
    options.Predicate = registration =>
        registration.Tags.Contains(monitorOptions.HealthChecks.Publisher.RequiredTag);
});

if (monitorOptions.Dashboard.Urls.Count > 0)
{
    foreach (var url in monitorOptions.Dashboard.Urls)
    {
        builder.WebHost.UseUrls(url);
    }
}

var app = builder.Build();

var endpoints = monitorOptions.HealthChecks.Endpoints;
app.MapHealthChecks(endpoints.All, new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = WriteHealthJsonAsync
});
app.MapHealthChecks(endpoints.Live, new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks(endpoints.Ready, new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(monitorOptions.HealthChecks.Publisher.RequiredTag),
    ResponseWriter = WriteHealthJsonAsync
});

app.MapGet("/healthz/plugins/{pluginId}", async (
    string pluginId,
    HealthCheckService healthCheckService,
    CancellationToken cancellationToken) =>
{
    var report = await healthCheckService.CheckHealthAsync(
        registration => registration.Tags.Contains(pluginId),
        cancellationToken);

    return Results.Json(new
    {
        status = report.Status.ToString(),
        entries = report.Entries.Select(x => new
        {
            name = x.Key,
            status = x.Value.Status.ToString(),
            description = x.Value.Description,
            durationMs = x.Value.Duration.TotalMilliseconds
        })
    });
});

if (monitorOptions.Dashboard.Enabled)
{
    app.MapMonitorDashboard();
}

app.Run();

static Task WriteHealthJsonAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return JsonSerializer.SerializeAsync(context.Response.Body, new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        entries = report.Entries.Select(x => new
        {
            name = x.Key,
            status = x.Value.Status.ToString(),
            description = x.Value.Description,
            durationMs = x.Value.Duration.TotalMilliseconds,
            data = x.Value.Data
        })
    });
}
