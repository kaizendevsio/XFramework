using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;
using XFramework.Domain.Auditing;
using XFramework.Domain.Shared.Interfaces;

namespace Notifications.Api.Installers;

public sealed class DbInstaller : IInstaller
{
    public void InstallServices<TAssembly>(
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
            .ConfigureWarnings(warnings => warnings.Ignore(
                RelationalEventId.BoolWithDefaultWarning,
                RelationalEventId.PendingModelChangesWarning))
            .AddXFrameworkAuditInterceptors(serviceProvider));

        services.AddServerDataContext<AppDbContext>();
    }
}
