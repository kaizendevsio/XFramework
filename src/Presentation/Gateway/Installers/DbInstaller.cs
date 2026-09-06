using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Domain.Auditing;

namespace Gateway.Installers;

public sealed class DbInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkAuditing();
        
        // Register DbContext with proper dependency injection for HttpContextAccessor and AuditInterceptor
        services.AddDbContext<DbContext, AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(string.IsNullOrEmpty(configuration["DefaultDatabaseConnection"])
                ? configuration.GetConnectionString("DefaultDatabaseConnection")
                : configuration["DefaultDatabaseConnection"],
                npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
            .AddXFrameworkAuditInterceptors(serviceProvider)
        );
    }
}
