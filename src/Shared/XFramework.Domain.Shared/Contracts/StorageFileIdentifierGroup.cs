namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageFileIdentifierGroup : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<StorageFileIdentifier> StorageFileIdentifiers { get; set; } =
        new List<StorageFileIdentifier>();
}
