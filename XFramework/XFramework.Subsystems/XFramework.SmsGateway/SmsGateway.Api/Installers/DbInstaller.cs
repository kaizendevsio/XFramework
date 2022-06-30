using SmsGateway.Core.DataAccess;
using SmsGateway.Core.Interfaces;
using SmsGateway.Domain.DataTransferObjects;

namespace SmsGateway.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
        services.AddTransient<IDataLayer, DataLayer>();
    }
}