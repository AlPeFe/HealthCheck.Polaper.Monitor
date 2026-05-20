# GamMonitorService Migration Architecture

## Overview

The migrated application should be a single .NET 10 monolith built on the generic host.

It contains multiple layers and plugin-style check modules, but it is deployed as one executable. The architecture should favor straightforward code over framework-heavy abstractions.

## Proposed Solution Structure

```text
GamMonitorService.sln
src/
  GamMonitorService.App/
  GamMonitorService.Domain/
  GamMonitorService.Application/
  GamMonitorService.Infrastructure/
  GamMonitorService.Plugins/
  GamMonitorService.Dashboard/
tests/
  GamMonitorService.Tests/
```

## Layer Responsibilities

### App Layer

Project: `GamMonitorService.App`

Responsibilities:

- Application entry point.
- Generic host setup.
- Windows Service integration.
- Serilog bootstrap.
- Configuration binding.
- Dependency Injection composition root.
- ASP.NET Core Health Checks registration.
- Health Check Publisher registration.
- Background worker registration.
- Dashboard hosting.

Recommended packages:

- `Microsoft.Extensions.Hosting`
- `Microsoft.Extensions.Hosting.WindowsServices`
- `Microsoft.Extensions.Diagnostics.HealthChecks`
- `Serilog.AspNetCore`
- `Serilog.Settings.Configuration`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`

### Domain Layer

Project: `GamMonitorService.Domain`

Responsibilities:

- Core models.
- Check status model.
- Plugin contracts.
- Alert state model.
- Domain rules that do not depend on infrastructure.

Core types:

```csharp
public enum CheckStatus
{
    Unknown = 0,
    Ok = 1,
    Warning = 2,
    Error = 3
}

public sealed record CheckResult(
    string PluginId,
    string ItemId,
    string Name,
    CheckStatus Status,
    string? Message,
    DateTimeOffset CheckedAt,
    TimeSpan Duration);

public interface ICheckPlugin
{
    string PluginId { get; }
    void RegisterHealthChecks(IHealthChecksBuilder builder, IConfiguration configuration);
}
```

Plugin implementations should use native ASP.NET Core `IHealthCheck` classes internally. Domain result models exist to normalize and persist state for alerting and dashboard display, not to replace the native health check abstraction.

### Application Layer

Project: `GamMonitorService.Application`

Responsibilities:

- Publishing native health check reports.
- Applying alert threshold rules.
- Applying email throttle rules.
- Storing latest results.
- Publishing dashboard state.
- Coordinating recovery alerts.

Important services:

- `MonitorHealthCheckPublisher`
- `AlertDecisionService`
- `NotificationService`
- `CheckStateService`

### Infrastructure Layer

Project: `GamMonitorService.Infrastructure`

Responsibilities:

- MailKit email sender.
- SQLite persistence.
- System clock abstraction if needed.
- SQL Server connectivity.
- File system and certificate loading.
- Process inspection and process start.
- HTTP client setup.

Recommended packages:

- `MailKit`
- `Microsoft.Data.Sqlite`
- `Dapper` or EF Core SQLite, only if persistence becomes more than trivial
- `Microsoft.Data.SqlClient`

### Plugins Layer

Project: `GamMonitorService.Plugins`

Responsibilities:

- Implement all built-in check plugins.
- Implement native ASP.NET Core `IHealthCheck` classes.
- Register each configured item through `AddCheck`, `AddTypeActivatedCheck`, or equivalent registration helpers.
- Keep each plugin isolated by folder and options class.

Suggested layout:

```text
Plugins/
  Process/
    ProcessCheckPlugin.cs
    ProcessHealthCheck.cs
    ProcessCheckOptions.cs
  HttpEndpoint/
    HttpEndpointCheckPlugin.cs
    HttpEndpointHealthCheck.cs
    HttpEndpointCheckOptions.cs
  DiskSpace/
    DiskSpaceCheckPlugin.cs
    DiskSpaceHealthCheck.cs
    DiskSpaceCheckOptions.cs
  SqlServer/
    SqlServerCheckPlugin.cs
    SqlServerHealthCheck.cs
    SqlServerCheckOptions.cs
  Certificate/
    CertificateCheckPlugin.cs
    CertificateHealthCheck.cs
    CertificateCheckOptions.cs
```

Plugin identifiers must be stable:

- `process`
- `http-endpoint`
- `disk-space`
- `sql-server`
- `certificate`

### Dashboard Layer

Project: `GamMonitorService.Dashboard`

Responsibilities:

- Expose a small visual dashboard.
- Expose optional JSON endpoints for diagnostics.
- Read from the latest persisted or in-memory check state.

Recommended approach:

- ASP.NET Core minimal APIs plus simple server-rendered HTML.
- No SPA.
- No authentication in first internal version unless deployment requires it.

Recommended endpoints:

- `GET /`
- `GET /dashboard`
- `GET /api/checks`
- `GET /api/health`

## Runtime Flow

1. Application starts through the .NET generic host.
2. Configuration is loaded from `appsettings.json`.
3. DI registers enabled plugins and their configured items as native ASP.NET Core health checks.
4. Health check endpoints are mapped with `MapHealthChecks()`.
5. `IHealthCheckPublisher` is registered.
6. `HealthCheckPublisherOptions` define automatic execution delay, period, timeout, and predicate.
7. The native health check publisher service executes matching checks periodically, 24/7.
8. `MonitorHealthCheckPublisher.PublishAsync()` receives the `HealthReport`.
9. Health reports are normalized into `CheckResult` values.
10. Result state is stored.
11. Alert decision service evaluates thresholds and throttle windows.
12. MailKit sender sends alert or recovery emails when required.
13. Dashboard reads latest state and may also expose custom health check JSON.

## Native Health Checks

The migration must use ASP.NET Core Health Checks as the native mechanism for check execution and status reporting.

Reference: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0

Required usage:

- Register services with `AddHealthChecks()`.
- Implement check logic through `IHealthCheck`.
- Return `HealthCheckResult.Healthy`, `HealthCheckResult.Degraded`, or `HealthCheckResult.Unhealthy`.
- Expose endpoints with `MapHealthChecks()`.
- Implement automatic background execution with `IHealthCheckPublisher`.
- Configure automatic execution with `HealthCheckPublisherOptions`.
- Use tags to identify plugin type, item id, and operational grouping.
- Use `HealthCheckOptions.Predicate` to filter checks by plugin or tag when needed.
- Use `HealthCheckOptions.ResponseWriter` for dashboard/API JSON output.

Recommended endpoint layout:

```text
GET /healthz
GET /healthz/live
GET /healthz/ready
GET /healthz/plugins/{pluginId}
GET /api/checks
```

Status mapping:

```text
HealthStatus.Healthy   -> CheckStatus.Ok
HealthStatus.Degraded  -> CheckStatus.Warning
HealthStatus.Unhealthy -> CheckStatus.Error
```

The app-specific alerting and dashboard state should wrap the native health check reports rather than replacing them.

## Automatic Publisher Model

Use the native Health Check Publisher model for automatic 24/7 execution.

Recommended approach:

- Configure default execution through `HealthCheckPublisherOptions`.
- Implement `MonitorHealthCheckPublisher : IHealthCheckPublisher`.
- In `PublishAsync`, update persisted state, dashboard state, and alert decisions.
- Use `Predicate` to execute only checks tagged for automatic monitoring.
- Use per-registration `HealthCheckRegistration.Delay` and `HealthCheckRegistration.Period` only when a specific item needs a different schedule from the global publisher period.

Example:

```csharp
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.FromSeconds(10);
    options.Period = TimeSpan.FromMinutes(5);
    options.Timeout = TimeSpan.FromSeconds(30);
    options.Predicate = check => check.Tags.Contains("monitor");
});

builder.Services.AddSingleton<IHealthCheckPublisher, MonitorHealthCheckPublisher>();
```

Avoid one `System.Timers.Timer` per check type and avoid a custom scheduler unless native publisher capabilities are insufficient.

Concurrency rules:

- The same item must not run concurrently with itself.
- Different items may run concurrently with a configurable max degree of parallelism.
- Default max concurrency should be conservative.

## Configuration-Driven Registration

Checks are opt-in by configuration.

Rules:

- Disabled plugins are not registered.
- Plugins with empty item lists are not registered.
- Disabled items are not registered.
- Enabled items are registered as named health checks.
- Health check names should be stable and include plugin id plus item id, for example `http-endpoint:main-api`.
- Tags should include at least `monitor`, plugin id, and item id.

This lets the user choose what is checked without needing to explicitly disable every unused plugin.

## Alert State Model

Alert state should be tracked per plugin id and item id.

Required fields:

- `PluginId`
- `ItemId`
- `CurrentStatus`
- `ConsecutiveFailures`
- `LastCheckedAt`
- `LastSuccessAt`
- `LastFailureAt`
- `LastAlertSentAt`
- `LastRecoverySentAt`
- `LastMessage`

Alert decision rules:

- OK result resets `ConsecutiveFailures`.
- ERROR result increments `ConsecutiveFailures`.
- WARNING may either increment or be handled separately depending on plugin settings.
- Send failure alert when `ConsecutiveFailures >= FailureThreshold` and no alert has been sent in the last 30 minutes.
- Send recovery alert when previous state was unhealthy and current state is OK.

Default values:

- `FailureThreshold = 1`
- `AlertThrottle = 00:30:00`

## Persistence Design

Use SQLite for latest state and alert throttle state.

Suggested tables:

```sql
CREATE TABLE CheckStates (
    PluginId TEXT NOT NULL,
    ItemId TEXT NOT NULL,
    Name TEXT NOT NULL,
    Status TEXT NOT NULL,
    ConsecutiveFailures INTEGER NOT NULL,
    LastCheckedAt TEXT NULL,
    LastSuccessAt TEXT NULL,
    LastFailureAt TEXT NULL,
    LastAlertSentAt TEXT NULL,
    LastRecoverySentAt TEXT NULL,
    LastMessage TEXT NULL,
    PRIMARY KEY (PluginId, ItemId)
);

CREATE TABLE CheckHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PluginId TEXT NOT NULL,
    ItemId TEXT NOT NULL,
    Status TEXT NOT NULL,
    Message TEXT NULL,
    CheckedAt TEXT NOT NULL,
    DurationMs INTEGER NOT NULL
);
```

`CheckHistory` is optional and should be capped or periodically cleaned if enabled.

## Configuration Model

Use strongly typed options.

Suggested root:

```csharp
public sealed class MonitorOptions
{
    public string InstanceId { get; init; } = "";
    public AlertOptions Alerts { get; init; } = new();
    public DashboardOptions Dashboard { get; init; } = new();
    public PluginOptions Plugins { get; init; } = new();
}
```

Every plugin options class should contain:

- `PluginId`
- `Enabled`
- `DefaultInterval`
- `Items`

Every item options class should contain:

- `Id`
- `Name`
- `Enabled`
- Plugin-specific fields

Validate configuration on startup. Invalid required fields should fail fast.

## Dependency Injection

Register dependencies by layer:

- Domain has no registrations.
- Application registers orchestration services.
- Infrastructure registers adapters.
- Plugins register implementations of `ICheckPlugin`.
- Dashboard registers routes and state readers.

Use constructor injection everywhere.

Avoid service locator patterns.

## Logging Architecture

Use Serilog from application startup.

Recommended enrichment properties:

- `Application`
- `InstanceId`
- `PluginId`
- `ItemId`
- `CheckStatus`

Recommended sinks:

- Console
- Rolling file

Optional future sinks:

- Windows Event Log
- OpenTelemetry

## Dashboard Architecture

The dashboard should be hosted in the same process.

Recommended implementation:

- Minimal API route returns HTML.
- CSS can be inline or static file.
- `/api/checks` returns current state as JSON.

Dashboard reads from `ICheckStateReader`, never directly from plugin implementations.

## Service Installation

Use native .NET worker service support.

Possible install command:

```powershell
sc.exe create GamMonitorService binPath= "C:\Path\GamMonitorService.exe" start= auto
```

Possible uninstall command:

```powershell
sc.exe delete GamMonitorService
```

Packaging can later add helper scripts, but the application should not depend on Topshelf.

## Migration Notes From Current Codebase

Replace:

- Topshelf with .NET generic host and Windows Service integration.
- NLog with Serilog.
- `System.Timers.Timer` orchestration with native Health Check Publisher execution.
- `App.config` and user settings with `appsettings.json`.
- Ad hoc email calls with centralized alert decision and notification services.
- Per-worker duplicate throttling dictionaries with persisted alert state.

Preserve:

- Background-first operation.
- Email notification model.
- Configurable process checks.
- Configurable endpoint checks.
- Configurable SQL checks.
- Configurable disk checks.
- Certificate expiration checks.

Improve:

- HTTP status acceptance model.
- TLS handling.
- Secret handling.
- Alert throttling.
- Dashboard visibility.
- Testability through DI and clear contracts.
