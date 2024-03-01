using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public record UpdateMessageReactionEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Emoji { get; set; }
}