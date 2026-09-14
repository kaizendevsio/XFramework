namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record EncryptionDevice
{
    public Guid DeviceId { get; set; }
    public string SigningPublicKey { get; set; } = "";
    public string EncryptionPublicKey { get; set; } = "";
    public string Approval { get; set; } = "";
    public string? Revocation { get; set; }
}

[MemoryPackable]
public partial record EncryptionDirectoryResponse
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public long Revision { get; set; }
    public string RootPublicKey { get; set; } = "";
    public string Roster { get; set; } = "";
    public List<EncryptionDevice> Devices { get; set; } = [];
}

[MemoryPackable]
public partial record EncryptionRecoveryResponse
{
    public long Revision { get; set; }
    public string? Archive { get; set; }
}
