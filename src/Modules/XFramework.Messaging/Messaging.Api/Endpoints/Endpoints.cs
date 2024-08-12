namespace Messaging.Api.Endpoints;

[GenerateEndpoints("XFramework.Domain.Shared.Contracts",new[] {
    nameof(Message),
    nameof(MessageType),
    nameof(MessageDelivery),
    nameof(MessageDeliveryType),
    nameof(MessageDirect),
    nameof(MessageFile),
    nameof(MessageReaction),
    nameof(MessageReactionType),
    nameof(MessageThread),
    nameof(MessageThreadType),
    nameof(MessageThreadMember),
    nameof(MessageThreadMemberRole),
    nameof(MessageThreadMemberGroup),
})]
public static partial class MessagingEndpoints;