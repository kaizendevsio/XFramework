using System.Security.Claims;
using System.Text.RegularExpressions;
using Yap.Contracts;

namespace Yap.Services;

// Hostnames are an operator allowlist, never a tenant selector supplied in a request body or forwarding header.
public sealed class YapTenants
{
    private readonly Dictionary<string, YapTenant> hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly YapTenant? legacy;
    public bool Mapped => hosts.Count != 0;
    public bool Configured => Mapped || legacy is not null;

    public YapTenants(IConfiguration configuration)
    {
        if (configuration["Yap:Hosts"] is not null)
            throw new InvalidOperationException("Yap:Hosts must be a hostname-to-workspace configuration object.");
        foreach (var section in configuration.GetSection("Yap:Hosts").GetChildren())
        {
            var host = section.Key;
            if (host != host.Trim() || Uri.CheckHostName(host) == UriHostNameType.Unknown || host.Contains('*') || host.EndsWith('.'))
                throw new InvalidOperationException($"Yap:Hosts:{host} must be an exact hostname without a port.");
            var tenant = ReadTenant(section) ?? throw new InvalidOperationException($"Yap:Hosts:{host} requires nonempty TenantId and RoleId.");
            tenant = tenant with { Branding = tenant.Branding with { ManifestUrl = "/api/branding/manifest" } };
            if (!hosts.TryAdd(host, tenant)) throw new InvalidOperationException($"Duplicate Yap hostname: {host}.");
        }
        if (!Mapped) legacy = ReadTenant(configuration.GetSection("Yap"));
    }

    public YapTenant? Resolve(HttpContext context) => Mapped
        ? hosts.GetValueOrDefault(context.Request.Host.Host) : legacy;

    public bool Matches(HttpContext context, ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated != true ||
        Resolve(context) is { } tenant && Guid.TryParse(user.FindFirstValue(YapAuth.TenantClaim), out var claimed) && claimed == tenant.TenantId;

    public void UseRouting(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // Container probes have no public tenant host and carry no tenant data.
            if (!context.Request.Path.StartsWithSegments("/health") && Mapped && Resolve(context) is null)
            {
                context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
                context.Response.Headers.CacheControl = "no-store";
                return;
            }
            await next(context);
        });
    }

    private static YapTenant? ReadTenant(IConfiguration section)
    {
        if (!Guid.TryParse(section["TenantId"], out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(section["RoleId"], out var role) || role == Guid.Empty) return null;
        var branding = section.GetSection("Branding");
        var defaults = YapBranding.Default;
        string Text(string field, string fallback, int max)
        {
            var value = branding[field]?.Trim() ?? fallback;
            if (value.Length == 0 || value.Length > max || value.Any(char.IsControl))
                throw new InvalidOperationException($"{branding.Path}:{field} must contain 1 to {max} printable characters.");
            return value;
        }
        string Image(string field, string fallback)
        {
            var value = branding[field] ?? fallback;
            if (value.Length == 0 && field == "LogoUrl") return value;
            if (!Regex.IsMatch(value, @"\A/(?:[a-zA-Z0-9_-]+/)*[a-zA-Z0-9_.-]+\.(?:png|svg|webp|jpg|jpeg)\z", RegexOptions.IgnoreCase) || value.Contains(".."))
                throw new InvalidOperationException($"{branding.Path}:{field} must be a same-origin absolute image path without queries or traversal.");
            return value;
        }
        var accent = branding["AccentColor"] ?? defaults.AccentColor;
        if (!Regex.IsMatch(accent, @"\A#[0-9a-fA-F]{6}\z"))
            throw new InvalidOperationException($"{branding.Path}:AccentColor must be a six-digit hex color.");
        return new(tenant, role, new(Text("Name", defaults.Name, 60), Text("ShortName", defaults.ShortName, 24),
            Text("Tagline", defaults.Tagline, 160), Image("LogoUrl", defaults.LogoUrl), accent,
            Image("Icon192Url", defaults.Icon192Url), Image("Icon512Url", defaults.Icon512Url), Image("AppleIconUrl", defaults.AppleIconUrl)));
    }
}

public sealed record YapTenant(Guid TenantId, Guid RoleId, YapBranding Branding);
