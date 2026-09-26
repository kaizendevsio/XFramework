using System.Runtime.CompilerServices;
using Bolt.Protocol;
using Microsoft.Extensions.Logging;

namespace Bolt.Server;

public sealed partial class BoltServer
{
    /// <summary>Largest heartbeat payload echoed; a send time and a counter fit comfortably.</summary>
    internal const int MaxHeartbeatPayloadBytes = 16;

    /// <summary>A client probing faster than this gets no extra echoes; the probe is a liveness check, not a pipe.</summary>
    internal const int MinHeartbeatSpacingMs = 200;

    private readonly ConditionalWeakTable<BoltHubConnection, StrongBox<long>> _heartbeatEchoTicks = new();

    /// <summary>
    /// Echo a <see cref="SignalType.Heartbeat"/> to its sender, and only to its sender. A phone uses
    /// the round trip to tell a slow link (echoes arrive late) from a dead one (echoes stop), which a
    /// browser WebSocket cannot tell it on its own after an IP change. The echo is queued like any
    /// other control frame, ahead of this receiver's media, and never awaited by the receive loop.
    /// </summary>
    private void EchoHeartbeat(BoltHubConnection sender, byte[] buffer, int length, in CallSignalHeader header)
    {
        if (header.PayloadLength > MaxHeartbeatPayloadBytes || !sender.IsAlive) return;
        var last = _heartbeatEchoTicks.GetValue(sender, static _ => new StrongBox<long>(long.MinValue / 2));
        var now = Environment.TickCount64;
        var previous = Interlocked.Read(ref last.Value);
        if (now - previous < MinHeartbeatSpacingMs || Interlocked.CompareExchange(ref last.Value, now, previous) != previous) return;
        var echo = buffer.AsSpan(0, length).ToArray();
        _ = SendHeartbeatEchoAsync(sender, echo);
    }

    private async Task SendHeartbeatEchoAsync(BoltHubConnection sender, byte[] echo)
    {
        try { await sender.SendAsync(echo, _shutdownCts.Token); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A connection too congested to take one control frame is exactly what the missing echo reports.
            _logger.LogDebug(ex, "Heartbeat echo to {ClientId} was not queued", sender.ClientId);
        }
    }
}
