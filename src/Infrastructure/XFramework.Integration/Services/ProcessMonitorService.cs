using System.Diagnostics;
using ByteSizeLib;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace XFramework.Integration.Services;

public class ProcessMonitorOptions
{
    public string MemoryLimit { get; set; }
}

public class ProcessMonitorService(IHostApplicationLifetime hostApplicationLifetime, IOptions<ProcessMonitorOptions> options, ILogger<ProcessMonitorService> logger)
{
    private  ByteSize _memoryLimit;

    public async Task ProcessMonitor(CancellationToken cancellationToken)
    {
        var tryParse = ByteSize.TryParse(options.Value.MemoryLimit, out _memoryLimit);
        if (!tryParse)
        {
            _memoryLimit = ByteSize.FromMegaBytes(500);
            logger.LogWarning("Invalid or missing MemoryLimit configuration. Defaulting to {DefaultMemoryLimit} MB", _memoryLimit.MegaBytes);
        }
        
        logger.LogInformation("Process Monitor Service Started with PID: {ProcessId}; Memory Limit: {MemoryLimit}MB", Environment.ProcessId, _memoryLimit.MegaBytes);
        
        hostApplicationLifetime.ApplicationStopping.Register(() => logger.LogInformation("Application is shutting down..."));
        hostApplicationLifetime.ApplicationStopped.Register(() => logger.LogInformation("Application terminated..."));
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var process = Process.GetCurrentProcess();
            var memory = ByteSize.FromBytes(process.PrivateMemorySize64);

            if (memory.MegaBytes > (_memoryLimit.MegaBytes - 10))
            {
                logger.LogInformation("Warning: Memory usage is {MemoryUsage} MB, getting closer to {MemoryLimit} MB limit. Application will be terminated when memory usage exceeds the limit", memory.MegaBytes, _memoryLimit.MegaBytes);
            }
            
            if (memory.MegaBytes > _memoryLimit.MegaBytes)
            {
                logger.LogInformation("Warning: Memory usage is {MemoryUsage} MB, exceeding limit {MemoryLimit} MB. Application will be terminated", memory.MegaBytes, _memoryLimit.MegaBytes);
                hostApplicationLifetime.StopApplication();
            }
            
            await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
        }
    }
}
