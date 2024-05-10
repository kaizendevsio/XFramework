namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityContentFile : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid ContentId { get; set; }

    [MemoryPackOrder(1)]
    public Guid StorageId { get; set; }


    [MemoryPackOrder(2)]
    public virtual CommunityContent Content { get; set; } = null!;

    [MemoryPackOrder(3)]
    public virtual StorageFile Storage { get; set; } = null!;
}
