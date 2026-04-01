namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ContentFileResponse
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public Guid StorageFileId { get; set; }
    public DateTime CreatedAt { get; set; }
}
