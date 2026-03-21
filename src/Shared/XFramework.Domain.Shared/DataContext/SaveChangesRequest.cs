namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class SaveChangesRequest
{
    [MemoryPackOrder(0)] public List<ChangeEntry> Changes { get; set; } = [];
}
