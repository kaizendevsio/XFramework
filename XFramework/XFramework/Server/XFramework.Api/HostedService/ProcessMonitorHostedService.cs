using System.Diagnostics;
using ByteSizeLib;
using XFramework.Integration.Services;

namespace XFramework.Api.HostedService
{
    public class ProcessMonitorHostedService : IHostedService
    {
        private readonly ProcessMonitorService _processMonitorService;

        public ProcessMonitorHostedService(ProcessMonitorService processMonitorService)
        {
            _processMonitorService = processMonitorService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //throw new System.NotImplementedException();
            _processMonitorService.ProcessMonitor();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //throw new System.NotImplementedException();
            return Task.CompletedTask;
        }
    }
}