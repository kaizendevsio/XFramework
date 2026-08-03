namespace IdentityServer.Domain.Shared.Contracts;

public sealed class VerificationDeliveryOutboxMessage : BaseModel
{
    public Guid VerificationId { get; set; }
    public Guid RequestId { get; set; }
    public int TransportType { get; set; }
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string? Intent { get; set; }
    public string? Message { get; set; }
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
