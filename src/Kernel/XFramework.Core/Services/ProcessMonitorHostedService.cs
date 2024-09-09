using Microsoft.Extensions.Hosting;
using XFramework.Integration.Services;

namespace XFramework.Core.Services;

public class ProcessMonitorHostedService(ProcessMonitorService processMonitorService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        //throw new System.NotImplementedException();
        _ = processMonitorService.ProcessMonitor(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //throw new System.NotImplementedException();
        return Task.CompletedTask;
    }
}