namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class TrackedEntity
{
    [MemoryPackOrder(0)] public string EntityTypeName { get; set; } = string.Empty;
    [MemoryPackOrder(1)] public Guid PrimaryKey { get; set; }
    [MemoryPackOrder(2)] public byte[] SnapshotBytes { get; set; } = [];

    [MemoryPackIgnore] public object? Snapshot { get; set; }
    [MemoryPackIgnore] public bool? OriginalIsEnabled { get; set; }
    [MemoryPackIgnore] public bool? OriginalIsDeleted { get; set; }
}
