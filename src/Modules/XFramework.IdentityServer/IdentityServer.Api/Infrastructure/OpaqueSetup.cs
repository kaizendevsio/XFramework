namespace IdentityServer.Api.Infrastructure;

/// <summary>Persistent OPAQUE setup. Provision once; never regenerate automatically on a deployed host.</summary>
public sealed class OpaqueSetup(IConfiguration configuration)
{
    public const string ServerIdentity = "XFramework.IdentityServer.OPAQUE.v1";
    private readonly Lazy<string> setup = new(() =>
    {
        var path = configuration["Opaque:SetupPath"];
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("OPAQUE setup is not configured.");
        var value = File.ReadAllText(path).Trim();
        if (value.Length is < 100 or > 2048) throw new InvalidOperationException("Invalid OPAQUE setup file.");
        OpaqueNative.Execute(new { operation = "validate-setup", setup = value });
        return value;
    });
    public string Value => setup.Value;
}

public sealed class OpaqueHealthCheck(OpaqueSetup setup) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { _ = setup.Value; return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()); }
        catch { return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("OPAQUE setup is unavailable.")); }
    }
}
