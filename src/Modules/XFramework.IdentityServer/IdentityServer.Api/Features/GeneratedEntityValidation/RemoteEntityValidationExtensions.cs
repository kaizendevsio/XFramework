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
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityRoleType>(provider.GetRequiredService<IValidator<IdentityRoleType>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityRoleTypeGroup>(provider.GetRequiredService<IValidator<IdentityRoleTypeGroup>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityContactType>(provider.GetRequiredService<IValidator<IdentityContactType>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityContactGroup>(provider.GetRequiredService<IValidator<IdentityContactGroup>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityAddressType>(provider.GetRequiredService<IValidator<IdentityAddressType>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<IdentityVerificationType>(provider.GetRequiredService<IValidator<IdentityVerificationType>>()));
        services.AddScoped<IRemoteDataContextEntityValidator>(provider =>
            new FluentRemoteDataContextEntityValidator<SessionType>(provider.GetRequiredService<IValidator<SessionType>>()));
        return services;
    }
}
