namespace Notifications.Domain.Shared.Contracts.Requests;

/// <summary>
/// Bypasses the delivery-job poller for events that are worthless late, such as a ringing call.
/// Carries routing identifiers only: the payload never contains message or caller content.
/// </summary>
[MemoryPackable]
public partial record SendDirectPushRequest : RequestBase,
    ICommand<QueryResponse<SendDirectPushResponse>>,
    IBoltRequest<SendDirectPushRequest, QueryResponse<SendDirectPushResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid RecipientCredentialId { get; set; }

    /// <summary>Client-interpreted push kind, for example "call" or "message".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Conversation the client should open when the notification is tapped.</summary>
    public Guid? ThreadId { get; set; }

    /// <summary>Opaque correlation the client uses to collapse and dismiss duplicate notifications.</summary>
    public string? Reference { get; set; }

    public int TimeToLiveSeconds { get; set; } = 30;

    /// <summary>
    /// Absolute moment the event stops being worth showing, for example when a ringing invite
    /// times out. A push service TTL only bounds how long delivery is attempted; this lets a
    /// worker that is woken late tell the difference between a live event and a stale one.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>RFC 8030 urgency. "high" keeps a ringing call out of a push service's batching window.</summary>
    public string Urgency { get; set; } = "high";
}
