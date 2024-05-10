namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageFile : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid StorageId { get; set; }


    [MemoryPackOrder(2)]
    public virtual Message Message { get; set; } = null!;

    [MemoryPackOrder(3)]
    public virtual StorageFile Storage { get; set; } = null!;
}
