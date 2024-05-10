namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityContentReaction : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid ContentId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(2)]
    public Guid SocialMediaIdentityId { get; set; }


    [MemoryPackOrder(3)]
    public virtual CommunityContent Content { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual CommunityContentReactionType Type { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
}
