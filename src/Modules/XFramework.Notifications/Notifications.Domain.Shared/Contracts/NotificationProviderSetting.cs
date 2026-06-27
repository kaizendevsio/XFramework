namespace Notifications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class NotificationProviderSetting : BaseModel
{
    [MemoryPackOrder(0)]
    public NotificationDeliveryChannel Channel { get; set; }

    [MemoryPackOrder(1)]
    public string ProviderKey { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string DisplayName { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public bool IsDefault { get; set; }

    [MemoryPackOrder(4)]
    public string? SettingsJson { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? LastHealthCheckAt { get; set; }

    [MemoryPackOrder(6)]
    public string? LastHealthStatus { get; set; }
}
