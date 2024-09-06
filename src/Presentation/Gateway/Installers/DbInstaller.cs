using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gateway.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddDbContext<DbContext, AppDbContext>(options => options
            .UseNpgsql(string.IsNullOrEmpty(configuration["DefaultDatabaseConnection"])
                ? configuration.GetConnectionString("DefaultDatabaseConnection")
                : configuration["DefaultDatabaseConnection"])
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
        );
    }
}