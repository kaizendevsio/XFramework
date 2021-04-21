using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Integration.Services.Helpers;

namespace Coins.Api.Installers
{
    public class HelperInstaller : IInstaller
    {
        public void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<HttpHelper>();
        }
    }
}