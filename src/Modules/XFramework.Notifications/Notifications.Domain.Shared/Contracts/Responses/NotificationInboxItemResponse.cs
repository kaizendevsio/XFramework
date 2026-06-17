namespace Notifications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record NotificationInboxItemResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public Guid? SourceCredentialId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationDeliveryChannel DeliveryChannels { get; set; }
    public string? CorrelationId { get; set; }
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
