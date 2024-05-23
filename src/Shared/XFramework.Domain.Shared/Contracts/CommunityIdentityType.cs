namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityIdentityType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<CommunityIdentity> CommunityIdentities { get; set; } = new List<CommunityIdentity>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
