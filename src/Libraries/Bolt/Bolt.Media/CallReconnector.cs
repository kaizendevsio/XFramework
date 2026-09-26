namespace Bolt.Media;

// BCL only, like CallLinkMonitor: the network harness compiles this file into its simulated phone.

/// <summary>What one resume attempt achieved.</summary>
public enum CallResumeAttempt
{
    /// <summary>The call is back on a new transport.</summary>
    Resumed,
    /// <summary>Not this time (network down, timeout, server busy); try again after the backoff.</summary>
    Retry,
    /// <summary>The server said no for good: the call ended, the seat expired, or access was revoked.</summary>
    Refused
}

/// <summary>How a whole reconnect ended.</summary>
public enum CallResumeOutcome { Resumed, GaveUp, Refused, Cancelled }

/// <summary>
/// Drives resume attempts for a <see cref="CallLinkMonitor"/> that is <see cref="CallLinkState.Reconnecting"/>:
/// the first attempt at once, then the jittered backoff (0.5, 1, 2, 3, 5 s, then every 5 s), until one
/// succeeds, the server refuses, or the grace period runs out. <see cref="Nudge"/> (a network hint)
/// cuts the current wait short. The clock, the wait and the jitter source are supplied by the caller,
/// so the timing is testable without real time passing.
/// </summary>
public sealed class CallReconnector(CallLinkMonitor link, Func<long> clock, Func<TimeSpan, CancellationToken, Task> delay, Func<double>? unit = null)
{
    private readonly Func<double> _unit = unit ?? Random.Shared.NextDouble;
    private readonly Lock _gate = new();
    private TaskCompletionSource _nudge = NewSignal();
    private bool _nudged;

    /// <summary>Attempts started so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>Start the next attempt now instead of after its backoff (the phone just regained a network).</summary>
    public void Nudge()
    {
        lock (_gate) { _nudged = true; _nudge.TrySetResult(); }
    }

    public async Task<CallResumeOutcome> RunAsync(Func<CancellationToken, Task<CallResumeAttempt>> attempt, CancellationToken ct)
    {
        for (var n = 0; ; n++)
        {
            if (ct.IsCancellationRequested) return CallResumeOutcome.Cancelled;
            var remaining = link.RemainingGraceMs(clock());
            if (remaining <= 0) return GiveUp();
            var wait = (int)Math.Min(link.BackoffDelayMs(n, _unit()), remaining);
            try { await WaitAsync(wait, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return CallResumeOutcome.Cancelled; }
            remaining = link.RemainingGraceMs(clock());
            if (remaining <= 0) return GiveUp();

            Attempts++;
            var result = await TryOnceAsync(attempt, (int)Math.Min(remaining, link.ResumeAttemptTimeoutMs), ct);
            if (ct.IsCancellationRequested) return CallResumeOutcome.Cancelled;
            switch (result)
            {
                case CallResumeAttempt.Resumed:
                    link.Resumed(clock());
                    return CallResumeOutcome.Resumed;
                case CallResumeAttempt.Refused:
                    link.Fail();
                    return CallResumeOutcome.Refused;
            }
        }
    }

    private CallResumeOutcome GiveUp() { link.Fail(); return CallResumeOutcome.GaveUp; }

    private async Task<CallResumeAttempt> TryOnceAsync(Func<CancellationToken, Task<CallResumeAttempt>> attempt, int timeoutMs, CancellationToken ct)
    {
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<CallResumeAttempt> work;
        try { work = attempt(scope.Token); }
        catch (Exception) when (!ct.IsCancellationRequested) { return CallResumeAttempt.Retry; }
        var timeout = delay(TimeSpan.FromMilliseconds(timeoutMs), scope.Token);
        var first = await Task.WhenAny(work, timeout);
        if (first != work)
        {
            // Too slow: abandon it. Whatever it was doing is cancelled, and its fault is observed here.
            scope.Cancel();
            _ = work.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
            return CallResumeAttempt.Retry;
        }
        scope.Cancel();
        _ = timeout.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
        try { return await work; }
        catch (Exception) when (!ct.IsCancellationRequested) { return CallResumeAttempt.Retry; }
    }

    private async Task WaitAsync(int ms, CancellationToken ct)
    {
        Task nudge;
        lock (_gate)
        {
            if (_nudged) { _nudged = false; return; }
            if (_nudge.Task.IsCompleted) _nudge = NewSignal();
            nudge = _nudge.Task;
        }
        if (ms <= 0) return;
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sleep = delay(TimeSpan.FromMilliseconds(ms), scope.Token);
        await Task.WhenAny(sleep, nudge);
        scope.Cancel();
        _ = sleep.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
        lock (_gate) _nudged = false;
        ct.ThrowIfCancellationRequested();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
