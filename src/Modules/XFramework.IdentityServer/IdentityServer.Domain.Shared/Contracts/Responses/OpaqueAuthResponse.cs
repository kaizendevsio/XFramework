namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record OpaqueAuthResponse
{
    public string? UserName { get; set; }
    public string Mode { get; set; } = "opaque";
    public Guid ExchangeId { get; set; }
    public string Client { get; set; } = "";
    public string Server { get; set; } = "XFramework.IdentityServer.OPAQUE.v1";
    public string? Message { get; set; }
    public string? WrappedRecovery { get; set; }
    public AuthenticateIdentityResponse? Authentication { get; set; }
}
