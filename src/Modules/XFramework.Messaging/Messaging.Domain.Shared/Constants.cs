namespace Messaging.Domain.Shared;

public static class MessageTypes
{
    public static readonly Guid Sms = new("f4fca110-790d-41d7-a0be-b5c699c9a9db");
    public static readonly Guid Email = new("67ee519a-babd-470a-88c5-cfcc578778ee");
    public static readonly Guid Push = new("073a033f-2c2d-4d18-8e27-85393e2a3592");
    public static readonly Guid Chat = new("d739a70a-dcf5-4707-b0a6-a8d1d39a42bf");
}

public static class MessageIntents
{
    public static readonly string Direct = nameof(Direct);
    public static readonly string Verification = nameof(Verification);
    public static readonly string Notification = nameof(Notification);
}

public static class MessageEvents
{
    public static readonly string SmsReceived = nameof(SmsReceived);
    public static readonly string EmailReceived = nameof(EmailReceived);
    public static readonly string PushReceived = nameof(PushReceived);
    public static readonly string ChatReceived = nameof(ChatReceived);
}

public static class MessageRealtimeEvents
{
    public static readonly string ThreadCreated = nameof(ThreadCreated);
    public static readonly string ThreadUpdated = nameof(ThreadUpdated);
    public static readonly string ThreadMemberAdded = nameof(ThreadMemberAdded);
    public static readonly string ThreadMemberRemoved = nameof(ThreadMemberRemoved);
    public static readonly string MessageCreated = nameof(MessageCreated);
    public static readonly string MessageEdited = nameof(MessageEdited);
    public static readonly string MessageDeleted = nameof(MessageDeleted);
    public static readonly string ReactionCreated = nameof(ReactionCreated);
    public static readonly string ReactionDeleted = nameof(ReactionDeleted);
    public static readonly string MessagesRead = nameof(MessagesRead);

    // TODO: Add audio/video call event types only after call/session feature flags exist in Messaging.
}


public static class GenericSender
{
    public static readonly string System = "+630000000000";
}

public static class MessageDeliveryTypes
{
    public static readonly Guid Delivered = new("b1000000-0000-0000-0000-000000000001");
    public static readonly Guid Read = new("b1000000-0000-0000-0000-000000000002");
}
