namespace IdentityServer.Domain.Shared.Contracts;

// Opaque public directory and encrypted archive only. No private key is persisted here.
public sealed class EncryptionAccount : IHasTenantId
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public long DirectoryRevision { get; set; }
    public string RootPublicKey { get; set; } = "";
    public string Roster { get; set; } = "";
    public string DevicesJson { get; set; } = "[]";
    public long RecoveryRevision { get; set; }
    public string? RecoveryArchive { get; set; }
}
