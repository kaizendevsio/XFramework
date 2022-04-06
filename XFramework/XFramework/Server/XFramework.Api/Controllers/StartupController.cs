using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace XFramework.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class StartupController : ControllerBase
    {
        [EnableQuery]
        [HttpGet]
        public ApiStatusBO Startup()
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

            return apiStatus;
        }
    }
}
