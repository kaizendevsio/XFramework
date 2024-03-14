
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityServer.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DbContext, AppDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString(configuration["DefaultDatabaseConnection"] ?? "DefaultConnection"))
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
        );
    }
}