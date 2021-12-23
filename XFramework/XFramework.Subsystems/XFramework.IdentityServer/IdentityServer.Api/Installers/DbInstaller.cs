using IdentityServer.Core.DataAccess;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Api.Installers
{
    public class DbInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<XFrameworkContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
            services.AddDbContext<LegacyContext>(options => options.UseNpgsql(configuration.GetConnectionString("LegacyDatabaseConnection")));
            services.AddTransient<IDataLayer, DataLayer>();
        }
    }
}