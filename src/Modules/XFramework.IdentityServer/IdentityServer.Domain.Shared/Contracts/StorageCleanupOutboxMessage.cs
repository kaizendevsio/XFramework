namespace IdentityServer.Domain.Shared.Contracts;

public sealed class StorageCleanupOutboxMessage : BaseModel
{
    public Guid StorageFileId { get; set; }
    public Guid RequestId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
}
