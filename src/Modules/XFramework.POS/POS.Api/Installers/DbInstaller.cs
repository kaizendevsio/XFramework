using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;
using XFramework.Domain.Auditing;
using XFramework.Domain.Shared.Interfaces;

namespace POS.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkAuditing();

        services.AddDbContext<DbContext, AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(string.IsNullOrEmpty(configuration["DefaultDatabaseConnection"])
                ? configuration.GetConnectionString("DefaultDatabaseConnection")
                : configuration["DefaultDatabaseConnection"],
                npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
            .AddXFrameworkAuditInterceptors(serviceProvider));

        services.AddServerDataContext<AppDbContext>();
    }
}
