using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageThreadMemberGroupRequest : RequestBase
{
    public short Status { get; set; }
    public string? Emoji { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public Guid? MessageThreadGuid { get; set; }
}