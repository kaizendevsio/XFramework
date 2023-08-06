using HealthEssentials.Core.DataAccess;
using HealthEssentials.Core.Interfaces;
using HealthEssentials.Domain.Contexts;

namespace HealthEssentials.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DefaultDatabaseConnection")), ServiceLifetime.Transient);
        services.AddDbContext<XnelSystemsHealthEssentialsContext>(options => options.UseNpgsql(configuration.GetConnectionString("HealthDatabaseConnection")), ServiceLifetime.Transient);
        services.AddTransient<IDataLayer, DataLayer>();
    }
}