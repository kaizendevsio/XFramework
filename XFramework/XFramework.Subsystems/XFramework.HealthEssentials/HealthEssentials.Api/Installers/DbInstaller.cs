using HealthEssentials.Core.DataAccess;
using HealthEssentials.Core.Interfaces;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DefaultDatabaseConnection")));
        services.AddDbContext<XnelSystemsHealthEssentialsContext>(options => options.UseNpgsql(configuration.GetConnectionString("HealthDatabaseConnection")));
        services.AddTransient<IDataLayer, DataLayer>();
    }
}