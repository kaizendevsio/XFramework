namespace Bolt.Media;

public enum CallStatus { Initiating, Ringing, Active, Held, Ended, Rejected, Missed }

public sealed class CallState
{
    public Guid CallId { get; set; }
    public CallStatus Status { get; set; }
    public string CallerClientId { get; set; } = "";
    public string CalleeClientId { get; set; } = "";
    public bool IsOutgoing { get; set; }
    public bool VideoRequested { get; set; }
    public Guid? AudioStreamId { get; set; }
    public Guid? VideoStreamId { get; set; }
    public bool IsDirectConnection { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int RingTimeoutSeconds { get; set; } = 30;
}
