namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class ChangeEntry
{
    [MemoryPackOrder(0)] public string EntityTypeName { get; set; } = string.Empty;
    [MemoryPackOrder(1)] public ChangeOperation Operation { get; set; }
    [MemoryPackOrder(2)] public byte[] SerializedEntity { get; set; } = [];
}
