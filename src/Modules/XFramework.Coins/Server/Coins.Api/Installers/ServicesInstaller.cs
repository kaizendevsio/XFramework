using Coins.Api.BusinessObjects;
using Coins.Api.Configurations;
using Coins.Api.Drivers;
using Coins.Api.Features.Blockchain.Send;
using Coins.Api.Interfaces;
using Coins.Api.Interfaces.Wrappers;
using Coins.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XFramework.Domain.Shared.Interfaces;

namespace Coins.Api.Installers;

/// <summary>
/// Services installer for Coins module
/// </summary>
public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register configuration
        var btcConfig = new BtcBlockchainConfiguration();
        configuration.GetSection(nameof(BtcBlockchainConfiguration)).Bind(btcConfig);
        services.AddSingleton(btcConfig);

        // Register HttpClient for blockchain operations
        services.AddHttpClient<IBtcBlockchainWrapper, BlockchainInfoDriver>();

        // Register services
        services.AddSingleton<ICachingService, CachingService>();
        
        // Register blockchain service
        services.AddScoped<IBlockchainService, BlockchainService>();

        // Register validators
        services.AddScoped<IValidator<List<BtcTransactionBO>>, SendTransactionsValidator>();
    }
}