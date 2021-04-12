using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Enums;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace IdentityServer.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class StartupController : ControllerBase
    {
        public IStreamFlowWrapper StreamFlowWrapper { get; }

        public StartupController(IStreamFlowWrapper streamFlowWrapper)
        {
            StreamFlowWrapper = streamFlowWrapper;
        }
        
        [HttpGet]
        public virtual async Task<ActionResult> Startup()
        {
            var apiStatus = new ApiStatusBO
            {
                ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name?.Split(".")[0],
                StartupTime = DateTime.Now,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                Host = new HostBO
                {
                    Platform = Environment.OSVersion.Platform.ToString(),
                    MachineName = Environment.MachineName,
                    ProccessorCount = Environment.ProcessorCount,
                    Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                    Is64BitProccess = Environment.Is64BitProcess,
                    SystemPageSize = Environment.SystemPageSize,
                    TickCount64 = Environment.TickCount64,
                    Version = Environment.OSVersion.ToString(),
                    RuntimeVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()
                        ?.FrameworkName
                },
                Status = "Running"
            };

            await StreamFlowWrapper.Push(new StreamFlowMessageBO()
            {
                MethodName = "TelemetryCall",
                Message = "Hello fucking world",
                ExchangeType = MessageExchangeType.Direct,
                Recipient = new Guid("3902761a-822d-4c6b-8e2d-323fd501b0d1")
            });
            
            return Ok(apiStatus);
        }
    }
}