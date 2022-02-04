namespace XFramework.Api.Installers
{
    public class DbInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            /*services.AddDbContext<XFrameworkContext>(options => options.UseNpgsql(configuration.GetConnectionString("DatabaseConnection")));
            services.AddScoped<IDataLayer, DataLayer>();*/
        }
    }
}