namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable]
public partial class ServiceSigningKey
{
    [MemoryPackOrder(0)] public Guid Id { get; set; }
    [MemoryPackOrder(1)] public string KeyId { get; set; } = string.Empty;
    [MemoryPackOrder(2)] public string Algorithm { get; set; } = "RS256";
    [MemoryPackOrder(3)] public string PrivateKeyPem { get; set; } = string.Empty;
    [MemoryPackOrder(4)] public string PublicKeyPem { get; set; } = string.Empty;
    [MemoryPackOrder(5)] public DateTime CreatedAtUtc { get; set; }
    [MemoryPackOrder(6)] public DateTime? ActivatedAtUtc { get; set; }
    [MemoryPackOrder(7)] public DateTime? RetiredAtUtc { get; set; }
    [MemoryPackOrder(8)] public bool IsActive { get; set; }
    [MemoryPackOrder(9)] public string? CreatedBy { get; set; }
}
