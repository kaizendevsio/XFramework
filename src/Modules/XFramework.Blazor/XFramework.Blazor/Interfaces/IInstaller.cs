using Microsoft.Extensions.DependencyInjection;

namespace XFramework.Blazor.Interfaces;

public interface IInstaller
{
    void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IWebAssemblyHostEnvironment webAssemblyHostEnvironment);
}