using System.Diagnostics;
using ByteSizeLib;
using MediatR;
using Microsoft.Extensions.Hosting;

namespace XFramework.Integration.Services;

public class ProcessMonitorService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _configuration;
    private ByteSize _memoryLimit;

    public ProcessMonitorService(IHostApplicationLifetime hostApplicationLifetime, IConfiguration configuration)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _configuration = configuration;
    }
    
    public async Task ProcessMonitor()
    {
        var tryParse = ByteSize.TryParse(_configuration.GetValue<string>("Application:MemoryLimit"), out _memoryLimit);
        if (!tryParse)
        {
            _memoryLimit = ByteSize.FromMegaBytes(500);
        }
        
        Console.WriteLine("*** Process Monitor Service Started with PID: {0}; Memory Limit: {1}MB", Environment.ProcessId, _memoryLimit.MegaBytes);
        
        _hostApplicationLifetime.ApplicationStopping.Register(() =>
        {
            Console.WriteLine("*** Application is shutting down...");
        }, true);
    
        _hostApplicationLifetime.ApplicationStopped.Register(() =>
        {
            Console.WriteLine("*** Application terminated...");
        }, true);
        
        while (true)
        {
            var process = Process.GetCurrentProcess();
            var memory = ByteSize.FromBytes(process.PrivateMemorySize64);
            //var cpu = process.TotalProcessorTime.TotalMilliseconds;
            //var cpuUsage = cpu / memory;
            //var memoryUsage = memory / cpu;

            if (memory.MegaBytes > (_memoryLimit.MegaBytes - 10))
            {
                Console.WriteLine("*** Warning: Memory usage is {0} MB, getting closer to {1} MB limit. Application will be terminated when memory usage exceeds the limit.", memory.MegaBytes, _memoryLimit.MegaBytes);
            }
            
            if (memory.MegaBytes > _memoryLimit.MegaBytes)
            {
                Console.WriteLine("*** Warning: Memory usage is {0} MB, exceeding limit {1} MB. Application will be terminated.", memory.MegaBytes, _memoryLimit.MegaBytes);
                _hostApplicationLifetime.StopApplication();
            }
                
            await Task.Delay(3000, CancellationToken.None);
        }
    }
}