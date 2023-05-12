using System.Reflection;
using System.Runtime.Versioning;
using IdentityServer.Integration.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers;

[Route("[controller]")]
[ApiController]
public class StartupController : ControllerBase
{
    private readonly IIdentityServiceWrapper _identityServiceWrapper;

    public StartupController(IIdentityServiceWrapper identityServiceWrapper)
    {
        _identityServiceWrapper = identityServiceWrapper;
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

        _identityServiceWrapper.AuthenticateCredential(new()
        {
            Username = null,
            Password = null,
        });
        return Ok(apiStatus);
    }
}