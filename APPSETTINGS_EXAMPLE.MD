# Appsettings Example

This file defines the intended configuration shape for the migrated service.

```json
{
  "Monitor": {
    "InstanceId": "CLIENTE-GAM-01",
    "Alerts": {
      "DefaultThrottle": "00:30:00",
      "Email": {
        "Enabled": true,
        "From": "monitor@example.com",
        "To": [ "operations@example.com" ],
        "SmtpHost": "smtp.example.com",
        "SmtpPort": 587,
        "Security": "StartTls",
        "Username": "monitor@example.com",
        "Password": ""
      }
    },
    "Dashboard": {
      "Enabled": true,
      "Urls": [ "http://localhost:5080" ]
    },
    "HealthChecks": {
      "Enabled": true,
      "Publisher": {
        "Delay": "00:00:10",
        "Period": "00:05:00",
        "Timeout": "00:00:30",
        "RequiredTag": "monitor"
      },
      "Endpoints": {
        "All": "/healthz",
        "Live": "/healthz/live",
        "Ready": "/healthz/ready"
      }
    },
    "Persistence": {
      "Provider": "Sqlite",
      "ConnectionString": "Data Source=gam-monitor-state.db",
      "EnableHistory": false,
      "HistoryRetentionDays": 7
    },
    "Plugins": {
      "Process": {
        "PluginId": "process",
        "Enabled": true,
        "DefaultInterval": "00:00:50",
        "Items": [
          {
            "Id": "gam-console",
            "Name": "GAM Console",
            "Enabled": true,
            "ProcessName": "gam",
            "ExecutablePath": "C:\\gam\\gam.exe",
            "RestartIfMissing": true,
            "FailureThreshold": 1
          }
        ]
      },
      "HttpEndpoint": {
        "PluginId": "http-endpoint",
        "Enabled": true,
        "DefaultInterval": "00:00:50",
        "Items": [
          {
            "Id": "main-api",
            "Name": "Main API",
            "Enabled": true,
            "Url": "https://api.example.com/",
            "Method": "GET",
            "Timeout": "00:00:30",
            "AcceptedStatusCodes": [ 200, 401, 403 ],
            "AcceptedStatusCodeRanges": [],
            "FailureThreshold": 3
          }
        ]
      },
      "DiskSpace": {
        "PluginId": "disk-space",
        "Enabled": true,
        "DefaultInterval": "00:05:00",
        "Items": [
          {
            "Id": "disk-c",
            "Name": "Disk C",
            "Enabled": true,
            "Path": "C:\\",
            "MinimumFreeGb": 20,
            "MinimumFreePercent": 10,
            "FailureThreshold": 1
          }
        ]
      },
      "SqlServer": {
        "PluginId": "sql-server",
        "Enabled": true,
        "DefaultInterval": "00:01:00",
        "Items": [
          {
            "Id": "cartografia-db",
            "Name": "Cartografia SQL Server",
            "Enabled": true,
            "ConnectionString": "Server=.;Initial Catalog=cartografia;Integrated Security=True;TrustServerCertificate=False;",
            "Timeout": "00:00:10",
            "Query": "SELECT 1",
            "ExpectedScalarResult": "1",
            "FailureThreshold": 2
          }
        ]
      },
      "Certificate": {
        "PluginId": "certificate",
        "Enabled": true,
        "DefaultInterval": "12:00:00",
        "Items": [
          {
            "Id": "client-cert",
            "Name": "Client Certificate",
            "Enabled": true,
            "Path": "C:\\certs\\client.pfx",
            "Password": "",
            "ExpirationWarningDays": 30,
            "FailureThreshold": 1
          }
        ]
      }
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/gam-monitor-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 14
        }
      }
    ],
    "Enrich": [ "FromLogContext" ],
    "Properties": {
      "Application": "GamMonitorService"
    }
  }
}
```
