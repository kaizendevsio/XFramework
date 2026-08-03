using Communications.Integration.Drivers;
using IdentityServer.Api.Infrastructure;
using Storage.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddCommunicationsWrapperServices();
        services.AddStorageWrapperServices();
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        services.AddScoped<IServiceIdentityService, ServiceIdentityService>();
        services.AddSingleton(serviceProvider => ServiceIdentityConfiguration.FromConfiguration(
            serviceProvider.GetRequiredService<IConfiguration>(),
            serviceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            serviceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName));
        services.AddSingleton<IBoltTransportTokenSigner, FileBackedBoltTransportTokenSigner>();
        services.AddSingleton<IIdentitySigningKeyProvider, IdentityServerLocalSigningKeyProvider>();
        services.AddIdentitySessionJwtValidation();
        XFramework.GeneratedServices.GeneratedEntityServiceRegistrations
            .AddGeneratedEntityServices(services);

        // Register AuthService for VSA pattern
        services.AddHostedService<PasswordResetOutboxDispatcher>();
        services.AddHostedService<VerificationDeliveryOutboxDispatcher>();
        services.AddHostedService<StorageCleanupOutboxDispatcher>();
        services.AddHostedService<StorageClaimOutboxDispatcher>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(serviceProvider => serviceProvider.GetRequiredService<AuthService>());
        services.AddScoped<IPasswordResetProcessor>(serviceProvider => serviceProvider.GetRequiredService<AuthService>());
        services.AddScoped<IIdentityAuthorizationService, IdentityAuthorizationService>();
        services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        services.AddScoped<Features.Tenants.ITenantAdministrationService,
            Features.Tenants.TenantAdministrationService>();
    }
}
