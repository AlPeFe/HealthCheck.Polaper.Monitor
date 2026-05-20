# GamMonitorService Migration Specification

## Purpose

Migrate the current GamMonitorService into a modern .NET 10 application that runs continuously in Windows environments and monitors operational dependencies for GAM installations.

The main value of the system is automatic background monitoring and email alerting. The dashboard is a lightweight visual aid for operators, not the primary notification mechanism.

## Goals

- Target .NET 10.
- Keep a monolithic deployable application.
- Use an N-layer architecture with clear boundaries.
- Model every type of check as a plugin-style module.
- Keep the implementation simple, explicit, and maintainable.
- Run both as a classic console app and as an installable Windows Service.
- Use built-in .NET hosting, dependency injection, configuration, and worker services.
- Use native ASP.NET Core Health Checks for check execution and health reporting.
- Use Serilog for structured logging.
- Use MailKit for email delivery.
- Use SQLite only if persistence is required.
- Replace Topshelf and NLog completely.
- Configure all checks through `appsettings.json`.
- Support N configured items per check type.
- Allow operators to choose which checks run through configuration.
- If a plugin is disabled or its item list is empty, no checks from that plugin should be registered or executed.
- Avoid repeated alert spam by throttling notifications.

## Non-Goals

- Do not migrate SQL maintenance scripts or schema management in the first version.
- Do not build a full observability platform.
- Do not require external infrastructure such as Prometheus, RabbitMQ, Redis, or a remote database.
- Do not introduce microservices.
- Do not introduce plugin loading from external assemblies unless explicitly required later.
- Do not require a frontend SPA unless the simple dashboard genuinely needs it.

## Runtime Modes

The application must support two execution modes with the same code path:

- Console mode: run interactively from terminal for development, diagnostics, and manual operation.
- Windows Service mode: run unattended 24/7 under the Windows Service Control Manager.

The application should use `Microsoft.Extensions.Hosting` and `UseWindowsService()` so the runtime automatically adapts when hosted as a service.

## Check Types

Each check type is implemented as a plugin-style component using the native ASP.NET Core Health Checks model.

Each plugin has a fixed identifier and may contain N configured items. Each configured item should be registered as an ASP.NET Core health check with a stable name and tags.

Reference: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0

The native Health Checks infrastructure is the primary contract for:

- Adding new check types.
- Registering configured check instances.
- Exposing health endpoints.
- Executing checks automatically on a period.
- Publishing health reports for alerting and dashboard state.

Do not create a parallel custom check engine that bypasses `IHealthCheck`.

Required plugin identifiers:

- `process`
- `http-endpoint`
- `disk-space`
- `sql-server`
- `certificate`

Future plugin identifiers may include:

- `tcp-port`
- `windows-service`
- `folder-age`
- `custom-script`

Script execution is explicitly excluded from the first migration scope.

## Process Check

Purpose: verify that configured Windows applications or console processes are running.

Configuration must support:

- Fixed item id.
- Display name.
- Process name.
- Optional executable path.
- Optional restart behavior.
- Check interval.
- Alert threshold.

Expected behavior:

- Mark OK when at least one matching process is running.
- If executable path is configured, prefer path validation when possible.
- Mark ERROR when the process is missing.
- Optionally attempt restart if enabled.
- Emit alert when the failure threshold is reached.

## HTTP Endpoint Check

Purpose: verify that APIs or web services respond in an acceptable way.

Configuration must support:

- Fixed item id.
- Display name.
- URL.
- HTTP method.
- Timeout.
- Optional headers.
- Optional client certificate.
- Accepted status codes or accepted status code ranges.
- Check interval.
- Alert threshold.

Expected behavior:

- A response can be considered OK even when it is not `200`.
- `401 Unauthorized` can be OK when configured as accepted.
- `403 Forbidden` can be OK when configured as accepted.
- `5xx` should normally be ERROR unless explicitly configured otherwise.
- Timeout, DNS failure, TLS failure, and connection failure are ERROR.

## Disk Space Check

Purpose: verify available disk space for configured drives.

Configuration must support:

- Fixed item id.
- Display name.
- Drive letter or mount path.
- Minimum free GB.
- Optional minimum free percentage.
- Check interval.
- Alert threshold.

Expected behavior:

- Mark OK when free space is above configured threshold.
- Mark ERROR when free space is below threshold.
- Include current free GB and percentage in check result details.

## SQL Server Check

Purpose: verify that SQL Server is reachable and can execute a simple query.

Configuration must support:

- Fixed item id.
- Display name.
- Connection string or named connection string reference.
- Timeout.
- Query, defaulting to `SELECT 1`.
- Expected scalar result, defaulting to `1`.
- Check interval.
- Alert threshold.

Expected behavior:

- Open SQL connection.
- Execute the configured lightweight query.
- Mark OK only when the query returns the expected result.
- Mark ERROR on connection failure, timeout, authentication failure, or unexpected result.

## Certificate Check

Purpose: verify certificate expiration for N configured certificates.

Configuration must support:

- Fixed item id.
- Display name.
- Certificate path.
- Optional password or secret reference.
- Expiration warning threshold in days.
- Check interval.
- Alert threshold.

Expected behavior:

- Mark OK when certificate is readable and expiration is outside the warning threshold.
- Mark ERROR when certificate cannot be read.
- Mark WARNING or ERROR when expiration is within threshold. The chosen status model is defined in `ARCHITECTURE.MD`.

## Alerting

Alerts are sent by email through MailKit.

Alerting rules:

- Do not send an email on every failed check execution.
- Once a check item reaches its alert threshold, send one alert.
- While the same item remains unhealthy, send at most one reminder every 30 minutes.
- When the item recovers, send one recovery email.
- After recovery, reset the alert state.

Default alert throttle:

- `30 minutes`

Alert emails must include:

- Application instance id.
- Plugin id.
- Check item id.
- Check item display name.
- Current status.
- Failure reason.
- Timestamp.
- Last successful check timestamp when available.

Alerting must be based on normalized health check results. Native Health Check statuses should map into the service status model:

- `Healthy` -> `OK`
- `Degraded` -> `WARNING`
- `Unhealthy` -> `ERROR`

## Dashboard

The dashboard is a small visual aid.

It must show:

- Application instance id.
- Current service status.
- List of configured check items.
- Plugin id.
- Check item name.
- Last execution timestamp.
- Last status: OK, WARNING, ERROR, UNKNOWN.
- Last error message or summary.

The dashboard must use the same native Health Check result pipeline as the background monitor. It may expose a custom JSON response for health status using `HealthCheckOptions.ResponseWriter`.

The dashboard may be implemented as:

- Minimal ASP.NET Core endpoints plus server-rendered HTML, or
- A minimal Razor Pages UI.

Keep it simple. Avoid a SPA unless requirements change.

## Persistence

Use SQLite only when needed.

Persistence is recommended for:

- Last check result per item.
- Alert throttle state.
- Last alert sent timestamp.
- Last recovery notification timestamp.
- Optional short check history.

Persistence is not required for:

- Full historical metrics.
- Long-term analytics.
- Audit-grade event storage.

If SQLite is used, it should be local to the service installation directory or a configured data directory.

## Configuration

Configuration source:

- `appsettings.json`
- `appsettings.{Environment}.json`
- environment variables for overrides

Every plugin type has:

- Fixed plugin id.
- Enabled flag.
- Default interval.
- N configured items.

Every item has:

- Stable id.
- Display name.
- Enabled flag.
- Item-specific settings.

Selection rules:

- The user chooses what is checked through `appsettings.json`.
- A plugin with `Enabled = false` is ignored.
- A plugin with no configured items is ignored.
- An item with `Enabled = false` is ignored.
- The user should not need to explicitly disable every unsupported plugin. Empty lists are enough.
- Only enabled configured items are registered into `AddHealthChecks()`.

Secrets must not be committed in plain text for production deployments.

## Automatic 24/7 Execution

The service must not only expose health endpoints for manual or external polling. It must also execute checks automatically while the process is running.

Use the native Health Check Publisher mechanism:

- Implement `IHealthCheckPublisher`.
- Register it in DI.
- Configure `HealthCheckPublisherOptions`.
- Use `Delay`, `Period`, `Timeout`, and `Predicate` to control automatic execution.

Example shape:

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

The publisher receives the `HealthReport`, normalizes it into application check state, persists the latest result if needed, updates dashboard state, and triggers alert decisions.

If different checks need different periods, prefer per-registration `HealthCheckRegistration.Delay` and `HealthCheckRegistration.Period` where appropriate.

## Logging

Use Serilog.

Logging requirements:

- Console sink for console execution.
- Rolling file sink for service execution.
- Structured properties for plugin id, check item id, status, duration, and exception.
- Avoid logging sensitive values such as passwords, certificate passwords, and full connection strings.

## Security

- Do not disable TLS validation globally.
- Do not store production passwords in committed `appsettings.json`.
- Mask connection strings and credentials in logs and emails.
- Prefer Windows-protected deployment secrets, environment variables, or another approved secret source.
- Limit process restart capability to explicitly configured paths.

## Acceptance Criteria

- The app builds on .NET 10.
- The app runs as console.
- The app can be installed and run as a Windows Service.
- Native ASP.NET Core Health Checks are used for check implementations and status reporting.
- Health checks are registered through `AddHealthChecks()`.
- Health check endpoints are exposed through `MapHealthChecks()`.
- Automatic repeated execution is implemented through `IHealthCheckPublisher` and `HealthCheckPublisherOptions`.
- Each configured check item is addressable by stable health check name and tags.
- Disabled plugins, disabled items, and empty item lists do not register checks.
- Serilog replaces NLog.
- Topshelf is not used.
- Dependency Injection is used for workers, plugins, alerting, persistence, logging, and configuration.
- Each check type is isolated behind a plugin-style contract.
- Each check type supports N configured items.
- Email alerts use MailKit.
- Alert throttling prevents repeated spam and defaults to 30 minutes.
- Dashboard shows last execution and status for every configured check item.
- Configuration is represented in `appsettings.json`.
- SQLite is used only for required runtime state.
