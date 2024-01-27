
using HealthEssentials.Domain.Contexts;

namespace HealthEssentials.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DbContext, HealthEssentialsContext>(options => options.UseNpgsql(configuration.GetConnectionString("HealthDatabaseConnection")));
    }
}