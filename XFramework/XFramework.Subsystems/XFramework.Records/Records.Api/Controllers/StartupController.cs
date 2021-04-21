using Microsoft.AspNetCore.Mvc;
using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Enums;
using StreamFlow.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace Records.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class StartupController : ControllerBase
    {
        public IMessageBusWrapper StreamFlowWrapper { get; }

        public StartupController(IMessageBusWrapper streamFlowWrapper)
        {
            StreamFlowWrapper = streamFlowWrapper;
        }
        
        [HttpGet]
        public async Task<ActionResult> Startup()
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
                    RuntimeVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
                },
                Status = "Running"
            };

            var entity = new CreateIdentityRequest()
            {
                FirstName = "Jay Test 111",
                LastName = "Eraldo"
            };
            await StreamFlowWrapper.Push(new(entity)
            {
                ExchangeType = MessageExchangeType.Direct,
                Recipient = new Guid("3902761a-822d-4c6b-8e2d-323fd501bcd6")
            });
            

            return Ok(apiStatus);
        }
    }
}
