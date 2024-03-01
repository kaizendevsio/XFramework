using Microsoft.Extensions.Hosting;
using XFramework.Integration.Services;

namespace XFramework.Core.Services;

public class ProcessMonitorHostedService(ProcessMonitorService processMonitorService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //throw new System.NotImplementedException();
        processMonitorService.ProcessMonitor(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //throw new System.NotImplementedException();
        return Task.CompletedTask;
    }
}