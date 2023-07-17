using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Core.DataAccess;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.DataTransferObjects;

namespace StreamFlow.Stream.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XFrameworkContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
        services.AddScoped<IDataLayer, DataLayer>();
    }
}