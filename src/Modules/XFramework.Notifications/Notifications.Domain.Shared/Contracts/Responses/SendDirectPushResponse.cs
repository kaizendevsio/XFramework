namespace Notifications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record SendDirectPushResponse
{
    public int Delivered { get; set; }
    public int Removed { get; set; }
    public int Failed { get; set; }
    public int Suppressed { get; set; }
}
