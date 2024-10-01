using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using XFramework.Blazor.Core.Extensions.WebAssembly;
using XFramework.Domain.Shared.Extensions;

// ReSharper disable once CheckNamespace
namespace XFramework.Extensions.WebAssembly;

public static class XApplication
{
    public static WebAssemblyHost Build<T>()
        where T : IComponent
    {
        var hostBuilder = Configure<T>();
        return hostBuilder.Build();
    }
    
    public static WebAssemblyHostBuilder Configure<T>()
        where T : IComponent
    {
        var hostBuilder = WebAssemblyHostBuilder.CreateDefault();
        hostBuilder.RootComponents.Add<T>("#app");
        hostBuilder.RootComponents.Add<HeadOutlet>("head::after");
        
        hostBuilder.Services.AddScoped<IHostEnvironment, WebAssemblyHostEnvironmentWrapper>();
        
        hostBuilder.Services.InstallServicesInAssembly<T>(hostBuilder.Configuration, hostBuilder.HostEnvironment.ToHostEnvironment());
        hostBuilder.Logging.AddSentry(o => o.InitializeSdk = false);
        
        return hostBuilder;
    }
    
    public static WebAssemblyHost UseBlazor<TApp>(this WebAssemblyHost app)
    {
        return app;
    }
}