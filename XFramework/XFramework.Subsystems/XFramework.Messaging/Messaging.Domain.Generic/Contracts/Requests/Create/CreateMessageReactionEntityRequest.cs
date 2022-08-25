using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public class CreateMessageReactionEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Emoji { get; set; }
}