namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PortalBootstrapAdminResponse
{
    public Guid TenantId { get; set; }
    public Guid IdentityId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid RoleTypeId { get; set; }
    public Guid RoleId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool Created { get; set; }
}
