using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace XFramework.Core.RateLimiting;

public static class TrustedProxyForwardingExtensions
{
    private const string KnownProxiesSection = "TrustedProxyForwarding:KnownProxies";

    public static IServiceCollection AddXFrameworkTrustedProxyForwarding(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredProxies = configuration
            .GetSection(KnownProxiesSection)
            .Get<string[]>() ?? [];
        var knownProxies = ResolveKnownProxies(configuredProxies);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var address in knownProxies)
                options.KnownProxies.Add(address);
        });

        return services;
    }

    public static IApplicationBuilder UseXFrameworkTrustedProxyForwarding(this IApplicationBuilder app)
    {
        app.UseForwardedHeaders();
        return app;
    }

    private static IReadOnlyCollection<IPAddress> ResolveKnownProxies(IEnumerable<string> configuredProxies)
    {
        HashSet<IPAddress> addresses = [IPAddress.Loopback, IPAddress.IPv6Loopback];

        foreach (var configuredProxy in configuredProxies)
        {
            var value = configuredProxy.Trim();
            if (string.IsNullOrEmpty(value))
                throw new InvalidOperationException($"{KnownProxiesSection} entries cannot be empty.");

            if (IPAddress.TryParse(value, out var address))
            {
                addresses.Add(address);
                continue;
            }

            IPAddress[] resolved;
            try
            {
                resolved = Dns.GetHostAddresses(value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"{KnownProxiesSection} contains an unresolvable proxy host.",
                    ex);
            }

            if (resolved.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{KnownProxiesSection} contains an unresolvable proxy host.");
            }

            addresses.UnionWith(resolved);
        }

        return addresses;
    }
}
