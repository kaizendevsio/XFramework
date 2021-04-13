using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XFramework.Api.Installers
{
    public interface IInstaller
    {
        void InstallServices(IServiceCollection services, IConfiguration configuration);
    }
}