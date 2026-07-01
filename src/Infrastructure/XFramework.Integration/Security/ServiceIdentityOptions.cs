namespace XFramework.Integration.Security;

public sealed class ServiceIdentityOptions
{
    public const string SectionName = "ServiceIdentity";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Issuer { get; set; } = "XFramework.IdentityServer";
    public int TokenRefreshSkewSeconds { get; set; } = 60;
    public int SigningKeyCacheMinutes { get; set; } = 15;
    public List<string> DefaultScopes { get; set; } = [];
}
