namespace Messaging.Domain.Shared.Contracts.Responses;

public record MessageTypeResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public short Priority { get; set; }
    public string? Guid { get; set; }
}