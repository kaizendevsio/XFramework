namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record RegisterIdentityResponse
{
    public Guid CredentialId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
}
