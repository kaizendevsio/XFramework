namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class StorageFileType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<StorageFile> StorageFiles { get; set; } = new List<StorageFile>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
