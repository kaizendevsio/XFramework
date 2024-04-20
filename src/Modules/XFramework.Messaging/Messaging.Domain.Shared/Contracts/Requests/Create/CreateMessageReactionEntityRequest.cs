using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageReactionEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Emoji { get; set; }
}