using Microsoft.Extensions.Hosting;
using XFramework.Integration.Services;

namespace XFramework.Core.Services;

public sealed class ProcessMonitorHostedService(ProcessMonitorService processMonitorService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return processMonitorService.ProcessMonitor(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}