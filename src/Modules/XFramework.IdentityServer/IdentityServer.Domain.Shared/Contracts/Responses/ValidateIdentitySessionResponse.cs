namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ValidateIdentitySessionResponse
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid SessionId { get; set; }
    public bool IsValid { get; set; }
}
