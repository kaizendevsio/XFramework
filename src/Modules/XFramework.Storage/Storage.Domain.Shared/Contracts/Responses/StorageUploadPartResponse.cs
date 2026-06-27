namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageUploadPartResponse
{
    public Guid Id { get; set; }
    public Guid UploadSessionId { get; set; }
    public int PartNumber { get; set; }
    public long OffsetBytes { get; set; }
    public int SizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string? ProviderPartId { get; set; }
    public bool WasAlreadyUploaded { get; set; }
    public DateTime UploadedAt { get; set; }
}
