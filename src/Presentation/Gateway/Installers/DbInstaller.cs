using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Domain.Interceptors;

namespace Gateway.Installers;

public sealed class DbInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register HttpContextAccessor for both audit tracking and global query filters (tenant context)
        services.AddHttpContextAccessor();
        
        // Register AuditInterceptor
        services.AddScoped<AuditInterceptor>();
        
        // Register DbContext with proper dependency injection for HttpContextAccessor and AuditInterceptor
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