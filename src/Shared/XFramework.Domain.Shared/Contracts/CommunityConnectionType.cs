namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityConnectionType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<CommunityConnection> CommunityConnections { get; set; } = new List<CommunityConnection>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
