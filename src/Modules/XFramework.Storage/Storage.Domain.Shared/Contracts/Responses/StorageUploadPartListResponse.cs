namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageUploadPartListResponse
{
    public Guid UploadSessionId { get; set; }
    public int TotalParts { get; set; }
    public List<StorageUploadPartResponse> Parts { get; set; } = [];
    public List<int> MissingPartNumbers { get; set; } = [];
}
