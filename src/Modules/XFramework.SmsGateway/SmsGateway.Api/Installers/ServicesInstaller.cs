using SmsGateway.Api.Services;
using XFramework.Domain.Shared.Interfaces;

namespace SmsGateway.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // CachingService MUST be Singleton - uses in-memory ConcurrentDictionary
        services.AddSingleton<ICachingService, CachingService>();
        
        // Register SMS service (VSA migration - replaced MediatR)
        services.AddScoped<ISmsService, SmsService>();
    }
}