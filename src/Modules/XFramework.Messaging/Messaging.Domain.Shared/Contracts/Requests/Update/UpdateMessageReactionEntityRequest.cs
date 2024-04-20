using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageReactionEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Emoji { get; set; }
}