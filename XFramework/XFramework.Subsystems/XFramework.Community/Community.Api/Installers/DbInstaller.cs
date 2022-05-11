using Community.Core.DataAccess;
using Community.Core.Interfaces;
using Community.Domain.DataTransferObjects;

namespace Community.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")), ServiceLifetime.Transient);
        services.AddTransient<IDataLayer, DataLayer>();
    }
}