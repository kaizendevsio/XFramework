namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record MessageFileResponse
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid StorageFileId { get; set; }
    public DateTime CreatedAt { get; set; }
}
