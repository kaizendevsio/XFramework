namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageFileIdentifier : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string? Description { get; set; }


    [MemoryPackOrder(2)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(3)]
    public virtual StorageFileIdentifierGroup Group { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual ICollection<StorageFile> StorageFiles { get; set; } = new List<StorageFile>();
}
