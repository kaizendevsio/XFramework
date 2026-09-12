namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetDeletedThreadsResponse
{
    public List<Guid> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
