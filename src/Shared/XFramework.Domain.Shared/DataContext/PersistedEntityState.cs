namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class PersistedEntityState
{
    [MemoryPackOrder(0)] public string EntityTypeName { get; set; } = string.Empty;
    [MemoryPackOrder(1)] public Guid EntityId { get; set; }
    [MemoryPackOrder(2)] public Guid? ConcurrencyStamp { get; set; }
}
