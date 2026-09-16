namespace IdentityServer.Domain.Shared.Contracts;

// Never expose through generated CRUD/remote queries. The backup contains ciphertext only.
public sealed class OpaqueCredential : IHasTenantId
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid Epoch { get; set; }
    public string Record { get; set; } = "";
    public string WrappedRecovery { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
