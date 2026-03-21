namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class GroupResult<TKey, TElement>
{
    [MemoryPackOrder(0)] public TKey Key { get; set; } = default!;
    [MemoryPackOrder(1)] public List<TElement> Items { get; set; } = [];
}
