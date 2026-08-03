using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Core.DataContext;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public static class RemoteEntityValidationExtensions
{
    public static IServiceCollection AddIdentityServerRemoteEntityValidation(this IServiceCollection services)
    {
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityAddress>(provider.GetRequiredService<IValidator<IdentityAddress>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityContact>(provider.GetRequiredService<IValidator<IdentityContact>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityFavorite>(provider.GetRequiredService<IValidator<IdentityFavorite>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<RegistryConfiguration>(provider.GetRequiredService<IValidator<RegistryConfiguration>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<RegistryConfigurationGroup>(
                provider.GetRequiredService<IValidator<RegistryConfigurationGroup>>()));
        return services;
    }
}
