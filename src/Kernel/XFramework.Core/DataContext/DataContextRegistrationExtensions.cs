using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.DataContext;

public static class DataContextRegistrationExtensions
{
    public static IServiceCollection AddServerDataContext<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<IDataContext, ServerDataContext<TDbContext>>();
        return services;
    }
}
