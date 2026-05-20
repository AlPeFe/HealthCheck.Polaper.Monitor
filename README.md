# GamMonitorService

Modern .NET 10 monolithic monitor for GAM operational dependencies.

The service can run as a console application or as a Windows Service. Checks are configured from `appsettings.json`, registered as native ASP.NET Core health checks, executed by `IHealthCheckPublisher`, stored in SQLite, and surfaced through a minimal dashboard.

## Run

```powershell
dotnet run --project src/GamMonitorService.App/GamMonitorService.App.csproj
```

Dashboard: `http://localhost:5080/dashboard`

Health endpoints:

- `/healthz`
- `/healthz/live`
- `/healthz/ready`
- `/healthz/plugins/{pluginId}`
- `/api/checks`
- `/api/health`

## Windows Service

Publish the app and register the produced executable:

```powershell
dotnet publish src/GamMonitorService.App/GamMonitorService.App.csproj -c Release -r win-x64 --self-contained false
sc.exe create GamMonitorService binPath= "C:\Path\GamMonitorService.App.exe" start= auto
```
