namespace Bolt.Media.Congestion;

/// <summary>
/// The first seconds of a picture. <see cref="Begin"/> places the send estimate and the ladder on what is known when the
/// camera starts (<see cref="StartRate.Choose"/>); for <see cref="WindowMs"/> after that, while the path has shown no
/// congestion, evidence that arrives later revises it: a receiver's downlink report (the worst receiver's, lowering the
/// rate before the queue it would cause) or this device's start probe (raising it to what the link carried). While the
/// picture is new, a limit measured exactly also caps the start-up ramp (<see cref="SendRateController.StartCeilingKbps"/>).
/// A guess never moves the estimate: once the ramp is running, only a measurement may. <see cref="Begin"/> is for a call's
/// first picture: what the audio-only path showed before it is forgotten.
/// </summary>
public sealed class PictureStart(SendRateController controller)
{
    /// <summary>How long after a picture starts late evidence may still revise its rate.</summary>
    public const int WindowMs = 10_000;

    private long _startedAt;
    private bool _active;
    private StartHints _hints, _applied;
    private int _audioWireKbps;

    /// <summary>The start rate, as last placed or revised (diagnostics).</summary>
    public StartEstimate? Estimate { get; private set; }

    /// <summary>Start a picture on <paramref name="hints"/> and the worst receiver's downlink; returns its first setting.</summary>
    public VideoSetting Begin(long nowMs, StartHints hints, int audioWireKbps, VideoRateLadder ladder, (int Kbps, bool AtLeast)? receivers)
    {
        _hints = _applied = WithReceivers(hints, receivers);
        _audioWireKbps = audioWireKbps;
        _startedAt = nowMs;
        _active = true;
        var estimate = Choose(_hints);
        controller.StartPicture(estimate.TotalKbps);
        Estimate = estimate with { TotalKbps = controller.EstimateKbps };
        controller.StartCeilingKbps = Ceiling(_hints);
        if (Ceiling(_hints) is { } limit) controller.MeasuredLimit(limit);
        return ladder.Start(Math.Max(0, controller.EstimateKbps - audioWireKbps));
    }

    /// <summary>This device's start probe finished.</summary>
    public void UplinkMeasured(int kbps, bool atLeast)
    {
        if (_active && kbps > 0) _hints = _hints with { UplinkKbps = kbps, UplinkAtLeast = atLeast };
    }

    /// <summary>One rate-loop tick, before the controller's update: apply evidence that changed.</summary>
    public void Tick(long nowMs, (int Kbps, bool AtLeast)? receivers)
    {
        if (!_active) return;
        if (nowMs - _startedAt > WindowMs || !controller.StartingUp)
        {
            _active = false;
            controller.StartCeilingKbps = null;
            return;
        }
        var hints = _hints = WithReceivers(_hints, receivers);
        if (hints == _applied) return;
        _applied = hints;
        controller.StartCeilingKbps = Ceiling(hints);
        if (Ceiling(hints) is { } limit) controller.MeasuredLimit(limit);
        var estimate = Choose(hints);
        // Only a measurement moves the rate (a guess never overrides the ramp): down at once, up to what was measured.
        if (estimate.Source is not (StartRate.UplinkProbe or StartRate.ReceiverDownlink or StartRate.NetworkHint)) return;
        if (controller.Revise(estimate.TotalKbps)) Estimate = estimate with { TotalKbps = controller.EstimateKbps };
    }

    private StartEstimate Choose(in StartHints hints) =>
        StartRate.Choose(hints, _audioWireKbps, controller.Options.MinTotalKbps, controller.Options.MaxTotalKbps);

    private static StartHints WithReceivers(StartHints hints, (int Kbps, bool AtLeast)? receivers) => receivers is { } down
        ? hints with { ReceiversDownlinkKbps = down.Kbps, ReceiversDownlinkAtLeast = down.AtLeast }
        : hints;

    /// <summary>A limit measured exactly (not a lower bound) is as far as the start-up ramp goes while the picture is new.</summary>
    private static int? Ceiling(in StartHints hints)
    {
        int? ceiling = null;
        if (hints.UplinkKbps is > 0 and var up && !hints.UplinkAtLeast) ceiling = up;
        if (hints.ReceiversDownlinkKbps is > 0 and var down && !hints.ReceiversDownlinkAtLeast) ceiling = Math.Min(ceiling ?? down, down);
        return ceiling;
    }
}
