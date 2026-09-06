using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XFramework.Domain.Interceptors;

namespace XFramework.Domain.Auditing;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddXFrameworkAuditing(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<AuditInterceptor>();
        services.TryAddScoped<AuditContextConnectionInterceptor>();
        return services;
    }

    public static DbContextOptionsBuilder AddXFrameworkAuditInterceptors(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider) =>
        options.AddInterceptors(
            serviceProvider.GetRequiredService<AuditInterceptor>(),
            serviceProvider.GetRequiredService<AuditContextConnectionInterceptor>());
}
