namespace IdentityServer.Domain.Shared.Contracts;

public sealed class PasswordResetOutboxMessage : BaseModel
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Guid RequestId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DispatchStartedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
}
