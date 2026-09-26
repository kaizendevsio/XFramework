namespace Yap.Client.Services;

/// <summary>The dot on a person's avatar. There is no dot at all for <see cref="Offline"/>.</summary>
public enum PresenceStatus { Offline, Away, Active }

/// <summary>
/// Turns the heartbeat the conversation header already shows into an avatar dot.
/// <list type="bullet">
/// <item><see cref="PresenceStatus.Active"/> (green): the same rule as "Active now" - a foreground
/// heartbeat inside its lifetime (the host's YapPresence.Lifetime, 45 s; a visible app beats every ~15-20 s).</item>
/// <item><see cref="PresenceStatus.Away"/> (orange): last seen within <see cref="AwayWindow"/>, the part
/// of "Last seen Nm ago" that is still recent.</item>
/// <item><see cref="PresenceStatus.Offline"/>: anything older, no heartbeat, or a hidden status.</item>
/// </list>
/// A person who hides active status in a conversation never has a heartbeat sent for it, so they read as offline.
/// </summary>
public static class PresenceDot
{
    public static readonly TimeSpan AwayWindow = TimeSpan.FromMinutes(15);

    public static PresenceStatus Of(DateTime? activeUntil, DateTime? lastActiveAt, DateTime utcNow) =>
        activeUntil is { } until && until > utcNow ? PresenceStatus.Active
        : lastActiveAt is { } last && utcNow - last < AwayWindow ? PresenceStatus.Away
        : PresenceStatus.Offline;

    /// <summary>What a screen reader hears with the avatar; null when there is nothing to say.</summary>
    public static string? Label(PresenceStatus status) => status switch
    {
        PresenceStatus.Active => "Active now",
        PresenceStatus.Away => "Away",
        _ => null
    };

    public static string? CssClass(PresenceStatus status) => status switch
    {
        PresenceStatus.Active => "active",
        PresenceStatus.Away => "away",
        _ => null
    };
}
