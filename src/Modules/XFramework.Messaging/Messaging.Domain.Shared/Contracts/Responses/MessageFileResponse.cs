
namespace Messaging.Domain.Shared.Contracts.Responses;

public record MessageFileResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Guid { get; set; }

    public MessageResponse? Message { get; set; }
    /*public StorageFileResponse? Storage { get; set; }*/
}