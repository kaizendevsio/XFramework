using System.Runtime.CompilerServices;
using Communications.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Domain.Interceptors;

namespace Bolt.Hub.Installers;

public sealed class DbInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // AppDbContext discovers mappings from loaded Domain.Shared assemblies. Load the
        // two authorization read-model assemblies before EF builds and caches the Hub model.
        RuntimeHelpers.RunClassConstructor(typeof(IdentityCredential).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(MessageThreadMember).TypeHandle);

        // Register HttpContextAccessor for audit tracking
        services.AddHttpContextAccessor();
        
        // Register AuditInterceptor
        services.AddScoped<AuditInterceptor>();
        
        services.AddDbContext<DbContext, AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(ResolveConnectionString(configuration),
                npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
            .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>())
        );

        services.AddServerDataContext<AppDbContext>();
    }

    public static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = FirstNonEmpty(
            configuration["DefaultDatabaseConnection"],
            configuration.GetConnectionString("DefaultDatabaseConnection"),
            configuration.GetConnectionString("DatabaseConnection"),
            configuration["DatabaseConnection"]);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Bolt Hub database connection is required. Configure DefaultDatabaseConnection or ConnectionStrings:DatabaseConnection.");
        }

        return connectionString;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
