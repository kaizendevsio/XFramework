namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageFileListResponse
{
    public List<StorageFileResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
