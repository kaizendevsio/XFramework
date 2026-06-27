namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetThreadListResponse
{
    public List<ThreadListItemResponse> Items { get; set; } = [];
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
