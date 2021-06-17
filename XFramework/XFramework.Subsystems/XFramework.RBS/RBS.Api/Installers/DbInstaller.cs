using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RBS.Core.DataAccess;
using RBS.Core.Interfaces;
using RBS.Domain.DataTransferObjects;

namespace RBS.Api.Installers
{
    public class DbInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<RBSContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
            services.AddScoped<IDataLayer, DataLayer>();
        }
    }
}