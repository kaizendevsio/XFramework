using IdentityServer.Core.DataAccess;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Records.Api.Installers
{
    public class DbInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<XFrameworkContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
            services.AddScoped<IDataLayer, DataLayer>();
        }
    }
}