namespace IdentityServer.Domain.Shared.Contracts.Requests;

public sealed record UpdateCredentialRequest : RequestBase
{
    public Guid? CredentialId { get; set; }
    public string? UserName { get; set; }
    public string? UserAlias { get; set; }
    public bool? IsEnabled { get; set; }
    public Guid ExpectedConcurrencyStamp { get; set; }
}
