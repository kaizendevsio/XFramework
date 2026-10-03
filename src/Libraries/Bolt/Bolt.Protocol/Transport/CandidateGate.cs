namespace Bolt.Protocol.Transport;

/// <summary>
/// Keeps a peer's own candidates behind the description they belong to. A WebRTC stack starts gathering when it
/// applies its local description, so its first candidates can be ready before that offer or answer has been sent.
/// If one overtook it, the other side would add it to the previous ICE generation (an ICE restart then drops it
/// with the rest), or drop it for lack of a session. Candidates are held from just before a description is made
/// until it has been sent, then go out in order.
/// </summary>
public sealed class CandidateGate
{
    private readonly object _sync = new();
    private List<RtcCandidate>? _held;

    /// <summary>A description is being made: hold candidates until <see cref="Release"/>.</summary>
    public void Hold()
    {
        lock (_sync) _held ??= [];
    }

    /// <summary>True when <paramref name="candidate"/> was held; false when it may be sent now.</summary>
    public bool TryHold(RtcCandidate candidate)
    {
        lock (_sync)
        {
            if (_held is null) return false;
            _held.Add(candidate);
            return true;
        }
    }

    /// <summary>The description has been sent: the candidates held meanwhile, in order. Later ones go straight out.</summary>
    public IReadOnlyList<RtcCandidate> Release()
    {
        lock (_sync)
        {
            var held = _held ?? [];
            _held = null;
            return held;
        }
    }
}
