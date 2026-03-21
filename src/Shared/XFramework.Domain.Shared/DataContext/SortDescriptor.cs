namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class SortDescriptor
{
    [MemoryPackOrder(0)] public string PropertyName { get; set; } = string.Empty;
    [MemoryPackOrder(1)] public bool Descending { get; set; }
    [MemoryPackOrder(2)] public bool IsSecondary { get; set; }
}
