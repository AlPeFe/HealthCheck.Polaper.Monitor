using GamMonitorService.Domain;

namespace GamMonitorService.Infrastructure;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
