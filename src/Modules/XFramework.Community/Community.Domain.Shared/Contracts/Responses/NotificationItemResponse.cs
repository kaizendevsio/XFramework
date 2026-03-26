using Community.Domain.Shared.Enums;

namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record NotificationItemResponse
{
    public Guid Id { get; set; }
    public Guid RecipientIdentityId { get; set; }
    public Guid ActorIdentityId { get; set; }
    public string? ActorHandleName { get; set; }
    public NotificationType Type { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Message { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
