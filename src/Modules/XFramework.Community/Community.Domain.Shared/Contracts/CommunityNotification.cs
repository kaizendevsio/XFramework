namespace Community.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityNotification : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid RecipientIdentityId { get; set; }

    [MemoryPackOrder(1)]
    public Guid ActorIdentityId { get; set; }

    [MemoryPackOrder(2)]
    public Guid? ContentId { get; set; }

    [MemoryPackOrder(3)]
    public string Type { get; set; } = null!;

    [MemoryPackOrder(4)]
    public string? Message { get; set; }

    [MemoryPackOrder(5)]
    public bool IsRead { get; set; }

    [MemoryPackOrder(6)]
    public virtual CommunityIdentity RecipientIdentity { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual CommunityIdentity ActorIdentity { get; set; } = null!;

    [MemoryPackOrder(8)]
    public virtual CommunityContent? Content { get; set; }
}
