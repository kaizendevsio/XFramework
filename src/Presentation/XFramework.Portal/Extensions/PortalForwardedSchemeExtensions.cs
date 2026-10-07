using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace XFramework.Portal.Extensions;

public static class PortalForwardedSchemeExtensions
{
    public static IServiceCollection AddPortalForwardedScheme(
        this IServiceCollection services, IConfiguration configuration)
    {
        var configuredAddress = configuration["Portal:TrustedProxyIp"];
        IPAddress? proxy = null;
        if (!string.IsNullOrWhiteSpace(configuredAddress)
            && (!IPAddress.TryParse(configuredAddress, out proxy)
                || proxy.Equals(IPAddress.Any) || proxy.Equals(IPAddress.IPv6Any)))
        {
            throw new InvalidOperationException("Portal:TrustedProxyIp must be a specific proxy IP address.");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = proxy is null ? ForwardedHeaders.None : ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            if (proxy is not null)
                options.KnownProxies.Add(proxy);
        });
        return services;
    }
}
