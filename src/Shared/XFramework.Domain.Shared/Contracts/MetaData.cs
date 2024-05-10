namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MetaData : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid KeyId { get; set; }

    [MemoryPackOrder(2)]
    public string? Value { get; set; }

    [MemoryPackOrder(3)]
    public virtual MetaDataType Type { get; set; } = null!;
}
