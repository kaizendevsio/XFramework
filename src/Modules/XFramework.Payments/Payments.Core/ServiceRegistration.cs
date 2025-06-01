using Microsoft.Extensions.DependencyInjection;
using Payments.Core.Services;

namespace Payments.Core;

public static class ServiceRegistration
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services, string baseCallbackUrl = "https://api.yourdomain.com")
    {
        // Register payment providers
        services.AddSingleton<IPaymentGatewayProvider, SamplePaymentProvider>();
        
        // Register payment gateway service
        services.AddSingleton<PaymentGatewayService>(sp => 
        {
            var providers = sp.GetServices<IPaymentGatewayProvider>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PaymentGatewayService>>();
            return new PaymentGatewayService(providers, logger, baseCallbackUrl);
        });
        
        return services;
    }
}
