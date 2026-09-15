namespace Notifications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PushConfigurationResponse
{
    /// <summary>False when no VAPID key pair is configured; clients must hide the feature rather than fail.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base64url application server key for PushManager.subscribe. Public by design.</summary>
    public string? VapidPublicKey { get; set; }

    /// <summary>Number of endpoints already registered for this credential across all its devices.</summary>
    public int SubscriptionCount { get; set; }
}
