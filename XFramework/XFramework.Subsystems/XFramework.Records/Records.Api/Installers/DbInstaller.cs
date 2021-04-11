using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Records.Core.DataAccess;
using Records.Core.Interfaces;
using Records.Domain.DataTransferObjects;

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