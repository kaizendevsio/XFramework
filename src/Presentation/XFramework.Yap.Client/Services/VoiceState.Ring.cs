using Microsoft.JSInterop;

namespace Yap.Client.Services;

/// <summary>Mirrors the JSON shapes returned by wwwroot/call-audio.js.</summary>
public sealed record RingTone(string Id, string Label);
public sealed record RingPreference(string Tone, double Volume);
public sealed record RingStatus(bool Audible, bool Vibrating);

public sealed partial class VoiceState
{
    // Ringing is derived from call state in exactly one place. Every exit path
    // (answer, decline, remote hangup, invite timeout, failure, account change,
    // dispose) already ends at Notify(), so none of them can leave a ring
    // playing over a connected call or after the call screen is gone.
    internal const string RingingStatus = "Ringing...";
    // A browser that never settles the start promise (autoplay policy does exactly that) must
    // not wedge the queue: the stop for accept or hangup is chained behind every start.
    internal static TimeSpan RingStartTimeout = TimeSpan.FromSeconds(4);
    private string ringing = "";
    private Task ringWork = Task.CompletedTask;
    /// <summary>The callee is being alerted, but this browser refused to play audio.</summary>
    public bool RingSilent { get; private set; }

    internal static string RingModeFor(bool calling, bool incoming, string status) =>
        !calling ? "" : incoming ? "ringtone" : status == RingingStatus ? "ringback" : "";

    private void Notify()
    {
        var mode = RingModeFor(active is not null, Incoming, Status);
        if (mode != ringing) { ringing = mode; ringWork = RingAsync(ringWork, mode); }
        Changed?.Invoke();
    }

    // Serialised so a start can never overtake the stop that was meant to precede it.
    private async Task RingAsync(Task previous, string mode)
    {
        try { await previous; } catch { }
        try
        {
            if (mode.Length == 0)
            {
                RingSilent = false;
                await js.InvokeVoidAsync("yap.ring.stop");
                return;
            }
            using var timeout = new CancellationTokenSource(RingStartTimeout);
            var status = await js.InvokeAsync<RingStatus>("yap.ring.start", timeout.Token, [mode]);
            // Only the incoming screen can offer the gesture that unblocks autoplay.
            if (ringing != mode) return;
            RingSilent = mode == "ringtone" && !status.Audible;
            Changed?.Invoke();
        }
        // A start that timed out is a ring nobody heard, so the incoming screen still has to
        // offer the retry gesture. Any other failure is decoration: a missing or blocked
        // AudioContext must never fail a call.
        catch (OperationCanceledException) { if (ringing == mode) { RingSilent = mode == "ringtone"; Changed?.Invoke(); } }
        catch { }
    }

    /// <summary>Retries the ring from a user gesture after autoplay blocked it.</summary>
    public async Task<bool> RetryRingAsync()
    {
        var mode = ringing;
        if (mode.Length == 0) return false;
        try
        {
            await js.InvokeAsync<bool>("yap.ring.unlock");
            var status = await js.InvokeAsync<RingStatus>("yap.ring.start", mode);
            if (ringing != mode) return false;
            RingSilent = !status.Audible;
            Changed?.Invoke();
            return status.Audible;
        }
        catch { return false; }
    }

    private void StopRing()
    { if (ringing.Length != 0) { ringing = ""; ringWork = RingAsync(ringWork, ""); } }
}
