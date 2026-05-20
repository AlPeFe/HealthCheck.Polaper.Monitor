using GamMonitorService.Domain;
using Microsoft.Extensions.Hosting;

namespace GamMonitorService.Infrastructure;

public sealed class DatabaseInitializerHostedService(ICheckStateStore store) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => store.InitializeAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
