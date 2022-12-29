using Microsoft.Extensions.DependencyInjection;

namespace XFramework.Client.Shared.Interfaces;

public interface IInstaller
{
    void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IWebAssemblyHostEnvironment webAssemblyHostEnvironment);
}