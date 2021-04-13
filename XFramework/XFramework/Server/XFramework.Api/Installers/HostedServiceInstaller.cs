using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XFramework.Api.Installers
{
    public class HostedServiceInstaller : IInstaller
    {
        public void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            //services.AddHostedService<SampleService>();
        }
    }
}