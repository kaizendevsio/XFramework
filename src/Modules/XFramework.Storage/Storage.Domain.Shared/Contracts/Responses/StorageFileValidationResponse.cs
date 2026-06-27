using XFramework.Domain.Shared.Contracts;

namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageFileValidationResponse
{
    public Guid StorageFileId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsValid { get; set; }
    public StorageFileStatus Status { get; set; }
    public StorageFileVisibility Visibility { get; set; }
    public Guid TypeId { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public long? ContentLengthBytes { get; set; }
    public string? Sha256Hash { get; set; }
    public string? Message { get; set; }
}
