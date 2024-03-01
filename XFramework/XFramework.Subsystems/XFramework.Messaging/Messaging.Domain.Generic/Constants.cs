namespace Messaging.Domain.Generic;

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


public static class GenericSender
{
    public static readonly string System = "+630000000000";
}