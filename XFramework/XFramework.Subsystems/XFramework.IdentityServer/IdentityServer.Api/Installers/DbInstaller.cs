using IdentityServer.Core.DataAccess;

namespace IdentityServer.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")), ServiceLifetime.Transient);
        services.AddTransient<IDataLayer, DataLayer>();
    }
}