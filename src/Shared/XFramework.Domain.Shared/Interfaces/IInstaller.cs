using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace XFramework.Domain.Shared.Interfaces;

public interface IInstaller
{
    void InstallServices<TAssembly>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment);
}