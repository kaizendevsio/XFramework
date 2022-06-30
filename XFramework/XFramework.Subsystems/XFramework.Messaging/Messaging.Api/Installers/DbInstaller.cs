using Messaging.Core.DataAccess;
using Messaging.Core.Interfaces;
using Messaging.Domain.DataTransferObjects;

namespace Messaging.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")), ServiceLifetime.Transient);
        services.AddTransient<IDataLayer, DataLayer>();
    }
}