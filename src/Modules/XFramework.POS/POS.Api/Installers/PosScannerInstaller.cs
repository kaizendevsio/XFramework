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
        var redisEndpoint = configuration["PosScanner:RedisConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisEndpoint))
            services.TryAddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisEndpoint);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 1000;
                options.ConnectRetry = 1;
                options.SyncTimeout = 1000;
                options.AsyncTimeout = 1000;
                return ConnectionMultiplexer.Connect(options);
            });
        services.TryAddSingleton<IDistributedSecurityRateLimiter>(sp =>
            new PosScannerClaimRateLimiter(() => sp.GetService<IConnectionMultiplexer>()));
        services.AddScoped<PosScannerService>();
    }
}
