namespace HealthEssentials.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XnelSystemsContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
        services.AddTransient<IDataLayer, DataLayer>();
    }
}