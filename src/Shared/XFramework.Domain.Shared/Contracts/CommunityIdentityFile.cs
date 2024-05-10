namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityIdentityFile : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid IdentityId { get; set; }

    [MemoryPackOrder(1)]
    public Guid StorageId { get; set; }

    [MemoryPackOrder(2)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(3)]
    public virtual CommunityIdentityFileType Type { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual CommunityIdentity Identity { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual StorageFile Storage { get; set; } = null!;
}
