namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class FieldPatch
{
    [MemoryPackOrder(0)] public byte[] EntityId { get; set; } = [];
    [MemoryPackOrder(1)] public Dictionary<string, byte[]> Changes { get; set; } = new();
    [MemoryPackOrder(2)] public Guid? ExpectedConcurrencyStamp { get; set; }
}
