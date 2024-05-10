namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityConnection : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid SourceSocialMediaIdentityId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TargetSocialMediaIdentityId { get; set; }

    [MemoryPackOrder(2)]
    public Guid TypeId { get; set; }


    [MemoryPackOrder(3)]
    public virtual CommunityConnectionType Type { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual CommunityIdentity SourceSocialMediaIdentity { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual CommunityIdentity TargetSocialMediaIdentity { get; set; } = null!;
}
