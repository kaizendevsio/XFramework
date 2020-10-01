using System;
using System.Reflection;
using XFramework.Data.BO;
using Microsoft.AspNetCore.Mvc;

namespace XFramework.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class StartupController : ControllerBase
    {
        [HttpGet]
        public ActionResult Startup()
        {
            ApiStatusBO apiStatus = new ApiStatusBO();
            apiStatus.ApplicationName = Assembly.GetEntryAssembly().GetName().Name.Split(".")[0];
            apiStatus.StartupTime = DateTime.Now;
            apiStatus.Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            apiStatus.Host = new HostBO();

            apiStatus.Host.Platform = Environment.OSVersion.Platform.ToString();
            apiStatus.Host.MachineName = Environment.MachineName;
            apiStatus.Host.ProccessorCount = Environment.ProcessorCount;
            apiStatus.Host.Is64BitOperatingSystem = Environment.Is64BitOperatingSystem;
            apiStatus.Host.Is64BitProccess = Environment.Is64BitProcess;
            apiStatus.Host.SystemPageSize = Environment.SystemPageSize;
            apiStatus.Host.TickCount64 = Environment.TickCount64;
            apiStatus.Host.Version = Environment.OSVersion.ToString();


            apiStatus.Status = "Running";

            return Ok(apiStatus);
        }

        [HttpGet("Test")]
        public string Test() {
            return "welcome";
        }
    }
}
