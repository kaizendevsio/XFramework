using Microsoft.Extensions.DependencyInjection;
using XFramework.Client.Shared.Interfaces;

namespace XFramework.Client.Shared.Extensions;

public static class InstallerExtensions
{
    public static void InstallServicesInAssembly<TApp>(this IServiceCollection services, IConfiguration configuration, IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
    {
        var installers = typeof(TApp).Assembly.ExportedTypes
            .Where(x => typeof(IInstaller).IsAssignableFrom(x) && x is {IsInterface: false, IsAbstract: false})
            .Select(Activator.CreateInstance)
            .Cast<IInstaller>()
            .ToList();
            
        installers.ForEach(installer => installer.InstallServices<TApp>(services, configuration, webAssemblyHostEnvironment));
    }
}