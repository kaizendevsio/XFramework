﻿namespace Messaging.Api.Generators;

[GenerateApiFromNamespace("XFramework.Domain.Generic.Contracts",new[] {
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
public static partial class MessagingApiGenerator;