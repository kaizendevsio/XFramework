namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MetaDataType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(2)]
    public int? SortOrder { get; set; }

    [MemoryPackOrder(3)]
    public virtual MetaDataTypeGroup Group { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual ICollection<MetaData> MetaData { get; set; } = new List<MetaData>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
