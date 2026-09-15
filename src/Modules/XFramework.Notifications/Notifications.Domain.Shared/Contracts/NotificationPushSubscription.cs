namespace Notifications.Domain.Shared.Contracts;

/// <summary>
/// One browser Push API subscription: a device+origin pair that a push service will deliver to.
/// Rows are per credential and per device, never per user, because the same person may install
/// the PWA on several devices and each one negotiates its own endpoint and key pair.
/// </summary>
[MemoryPackable(GenerateType.CircularReference)]
public partial class NotificationPushSubscription : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    /// <summary>The push service delivery URL. Treat as a bearer capability: anyone holding it can wake the device.</summary>
    [MemoryPackOrder(1)]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>SHA-256 of the endpoint, because endpoint URLs are too long to index directly.</summary>
    [MemoryPackOrder(2)]
    public string EndpointHash { get; set; } = string.Empty;

    /// <summary>Base64url uncompressed P-256 public key from PushSubscription.getKey('p256dh').</summary>
    [MemoryPackOrder(3)]
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Base64url 16-byte auth secret from PushSubscription.getKey('auth').</summary>
    [MemoryPackOrder(4)]
    public string Auth { get; set; } = string.Empty;

    /// <summary>Client-supplied device identity, so a revoked encryption device can drop its push rows too.</summary>
    [MemoryPackOrder(5)]
    public Guid? DeviceId { get; set; }

    /// <summary>Coarse client label for support; never a full user agent string.</summary>
    [MemoryPackOrder(6)]
    public string? DeviceLabel { get; set; }

    [MemoryPackOrder(7)]
    public DateTime? ExpiresAt { get; set; }

    [MemoryPackOrder(8)]
    public DateTime LastSeenAt { get; set; }

    [MemoryPackOrder(9)]
    public DateTime? LastSuccessAt { get; set; }

    [MemoryPackOrder(10)]
    public DateTime? LastFailureAt { get; set; }

    [MemoryPackOrder(11)]
    public int ConsecutiveFailureCount { get; set; }

    [MemoryPackOrder(12)]
    public string? LastErrorCode { get; set; }
}
