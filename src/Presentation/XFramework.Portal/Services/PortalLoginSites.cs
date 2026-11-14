using System.Security.Claims;

namespace XFramework.Portal.Services;

public sealed class PortalLoginSites
{
    private readonly PortalLoginSite _default;
    private readonly Dictionary<string, PortalLoginSite> _hosts = new(StringComparer.OrdinalIgnoreCase);

    public PortalLoginSites(IConfiguration configuration)
    {
        var section = configuration.GetSection("Portal:Login");
        _default = section.GetSection("Default").Get<PortalLoginSite>() ?? new();
        Validate(_default);
        foreach (var entry in section.GetSection("Hosts").GetChildren())
        {
            var site = entry.Get<PortalLoginSite>() ?? new();
            Validate(site);
            var host = entry["Host"]?.Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(host) || Uri.CheckHostName(host) == UriHostNameType.Unknown ||
                !_hosts.TryAdd(host, site))
                throw new InvalidOperationException("Portal login hosts must be unique hostnames without schemes, paths, or ports.");
        }
    }

    // Once host bindings exist, unknown hosts must never fall back to platform admin sign-in.
    public PortalLoginSite? Resolve(string host) => _hosts.Count == 0
        ? _default
        : _hosts.GetValueOrDefault(host.TrimEnd('.'));

    public bool AcceptsPrincipal(string host, ClaimsPrincipal? principal)
    {
        var site = Resolve(host);
        if (site is null)
            return false;
        if (_hosts.Count == 0 && !site.IsTenantLogin)
            return true;
        return Guid.TryParse(principal?.FindFirst(PortalAuthClaims.TenantId)?.Value, out var tenantId)
            && Guid.TryParse(principal?.FindFirst(PortalAuthClaims.RoleTypeId)?.Value, out var roleId)
            && tenantId == site.AuthenticationTenantId && roleId == site.AuthenticationRoleId;
    }

    private static void Validate(PortalLoginSite site)
    {
        if (site.TenantId.HasValue != site.RoleTypeId.HasValue ||
            site.TenantId == Guid.Empty || site.RoleTypeId == Guid.Empty ||
            (site.IsTenantLogin && (site.TenantId == PortalBootstrapConstants.AdminTenantId ||
                site.RoleTypeId == PortalBootstrapConstants.AdminRoleTypeId)))
            throw new InvalidOperationException("Tenant login requires both a tenant ID and its assigned non-platform role type ID.");

        if (site.LogoUrl is { Length: > 0 } logo &&
            !(logo.StartsWith('/') && !logo.StartsWith("//") && !logo.Contains('\\')) &&
            !(Uri.TryCreate(logo, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)))
            throw new InvalidOperationException("Portal login logos must use a local absolute path or an HTTPS URL.");
    }
}

public sealed class PortalLoginSite
{
    public Guid? TenantId { get; set; }
    public Guid? RoleTypeId { get; set; }
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsTenantLogin => TenantId.HasValue;
    public string BrandName => string.IsNullOrWhiteSpace(DisplayName) ? "XSystems" : DisplayName;
    public Guid AuthenticationTenantId => TenantId ?? PortalBootstrapConstants.AdminTenantId;
    public Guid AuthenticationRoleId => RoleTypeId ?? PortalBootstrapConstants.AdminRoleTypeId;
}
