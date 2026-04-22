using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class SaveChangesRequest
{
    [MemoryPackOrder(0)] public List<ChangeEntry> Changes { get; set; } = [];
    [MemoryPackOrder(1)] public RequestMetadata? Metadata { get; set; }
}
