using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XFramework.Blazor.Extensions;

public static class XApplication
{
    public static WebAssemblyHostBuilder Configure<T>()
        where T : IComponent
    {
        var hostBuilder = WebAssemblyHostBuilder.CreateDefault();
        hostBuilder.RootComponents.Add<T>("#app");
        hostBuilder.RootComponents.Add<HeadOutlet>("head::after");
        
        hostBuilder.Services.AddSingleton<IHostEnvironment>(new MockedHostEnvironment
        {
            EnvironmentName = hostBuilder.HostEnvironment.Environment,
            ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name?.Split(".")[0],
        });
        
        hostBuilder.Services.InstallServicesInAssembly<T>(hostBuilder.Configuration, hostBuilder.HostEnvironment);
        //hostBuilder.Services.InstallServicesInAssembly<MHI.Shared.Base>(hostBuilder.Configuration, hostBuilder.HostEnvironment);
        hostBuilder.Services.InstallServicesInAssembly<XFramework.Blazor.Base>(hostBuilder.Configuration, hostBuilder.HostEnvironment);
        
        hostBuilder.Logging.AddSentry(o => o.InitializeSdk = false);
        
        return hostBuilder;
    }
}