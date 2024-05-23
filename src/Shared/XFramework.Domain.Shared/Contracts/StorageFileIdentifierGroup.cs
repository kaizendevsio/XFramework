namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageFileIdentifierGroup : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<StorageFileIdentifier> StorageFileIdentifiers { get; set; } =
        new List<StorageFileIdentifier>();

    
    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
