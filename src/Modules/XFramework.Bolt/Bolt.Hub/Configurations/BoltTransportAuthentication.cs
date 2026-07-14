namespace Bolt.Hub.Configurations;

public sealed class BoltTransportAuthentication
{
    public const string SectionName = nameof(BoltTransportAuthentication);
    public const string Scheme = "BoltTransportBearer";
    public const string ExpectedIssuer = "XFramework.IdentityServer";
    public const string ExpectedAudience = "XFramework.Bolt.Hub";
    public const string ExpectedTokenType = "bolt+jwt";

    public string MetadataAddress { get; set; } = string.Empty;
    public string Issuer { get; set; } = ExpectedIssuer;
    public string Audience { get; set; } = ExpectedAudience;
    public bool RequireHttpsMetadata { get; set; } = true;
}
