using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallets.Core.DataAccess;
using Wallets.Core.Interfaces;
using Wallets.Domain.DataTransferObjects;

namespace Wallets.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")), ServiceLifetime.Transient);
        services.AddTransient<IDataLayer, DataLayer>();
    }
}