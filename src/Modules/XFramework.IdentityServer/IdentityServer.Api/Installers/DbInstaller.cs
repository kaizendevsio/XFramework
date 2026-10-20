
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;
using XFramework.Domain.Auditing;

namespace IdentityServer.Api.Installers;

public sealed class DbInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Finance settings project CurrencyType read-only; load its mappings before the first model is cached.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Wallets.Domain.Shared.Contracts.CurrencyType).TypeHandle);
        services.AddXFrameworkAuditing();
        
        services.AddDbContext<DbContext, AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(string.IsNullOrEmpty(configuration["DefaultDatabaseConnection"])
                ? configuration.GetConnectionString("DefaultDatabaseConnection")
                : configuration["DefaultDatabaseConnection"],
                npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
            .AddXFrameworkAuditInterceptors(serviceProvider)
        );

        services.AddServerDataContext<AppDbContext>();
    }
}
