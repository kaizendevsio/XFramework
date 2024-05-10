using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Messaging.Integration.Generators;

[StreamFlowWrapper("XFramework.Domain.Shared.Contracts",new[] {
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
public static class MessagingServiceWrapper;