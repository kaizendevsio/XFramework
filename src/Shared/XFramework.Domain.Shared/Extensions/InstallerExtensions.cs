using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XFramework.Domain.Shared.Interfaces;

namespace XFramework.Domain.Shared.Extensions;

public static class InstallerExtensions
{
    public static void InstallServicesInAssembly<TAssembly>(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var installers = typeof(TAssembly).Assembly.ExportedTypes
            .Where(x => typeof(IInstaller).IsAssignableFrom(x) && x is {IsInterface: false, IsAbstract: false})
            .Select(Activator.CreateInstance)
            .Cast<IInstaller>()
            .ToList();
        
        installers.ForEach(installer => installer.InstallServices<TAssembly>(services, configuration, hostEnvironment));
    }
}