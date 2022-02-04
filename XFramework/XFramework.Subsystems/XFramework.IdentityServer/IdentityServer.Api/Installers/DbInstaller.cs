using IdentityServer.Core.DataAccess;
using IdentityServer.Domain.DataTransferObjects.Legacy;

namespace IdentityServer.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
        services.AddDbContext<LegacyContext>(options => options.UseNpgsql(configuration.GetConnectionString("LegacyDatabaseConnection")));
        services.AddTransient<IDataLayer, DataLayer>();
    }
}