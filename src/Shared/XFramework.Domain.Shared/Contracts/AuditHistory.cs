namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class AuditHistory : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? TableName { get; set; }


    [MemoryPackOrder(1)]
    public string? KeyValues { get; set; }

    [MemoryPackOrder(2)]
    public string? OldValues { get; set; }

    [MemoryPackOrder(3)]
    public string? NewValues { get; set; }

    [MemoryPackOrder(4)]
    public string? QueryAction { get; set; }
}
