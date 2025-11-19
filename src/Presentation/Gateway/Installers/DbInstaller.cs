using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Domain.Interceptors;

namespace Gateway.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register HttpContextAccessor for audit tracking
        services.AddHttpContextAccessor();
        
        // Register AuditInterceptor
        services.AddScoped<AuditInterceptor>();
        
        services.AddDbContext<DbContext, AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(string.IsNullOrEmpty(configuration["DefaultDatabaseConnection"])
                ? configuration.GetConnectionString("DefaultDatabaseConnection")
                : configuration["DefaultDatabaseConnection"],
                npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
            .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>())
        );
    }
}