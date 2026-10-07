using Microsoft.Extensions.DependencyInjection.Extensions;
using POS.Api.Services;
using XFramework.Core.RateLimiting;
using StackExchange.Redis;
using XFramework.Domain.Shared.Interfaces;

namespace POS.Api.Installers;

public sealed class PosScannerInstaller : IInstaller
{
    public void InstallServices<TAssembly>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PosScannerPairingStore>();
        services.TryAddSingleton<IDistributedSecurityRateLimiter>(sp =>
            new PosScannerClaimRateLimiter(() => sp.GetService<IConnectionMultiplexer>()));
        services.AddScoped<PosScannerService>();
    }
}
