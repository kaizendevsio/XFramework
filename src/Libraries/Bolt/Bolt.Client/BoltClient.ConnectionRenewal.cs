using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using Bolt.Protocol;
using Microsoft.Extensions.Logging;

namespace Bolt.Client;

public sealed partial class BoltClient
{
    private readonly SemaphoreSlim _connectionRenewalGate = new(1, 1);
    private readonly HashSet<Task> _connectionRenewalTasks = [];
    private readonly ConcurrentDictionary<Guid, BoltConnection> _pendingCallConnections = new();

    // Called while holding _connectionsLock. Replacement handshakes are serialized, drains may overlap.
    private void StartConnectionRenewal(BoltConnection connection)
    {
        if (!_config.AutoReconnect || !_config.RenewConnectionsBeforeExpiry || _config.AccessTokenProvider is null)
            return;

        var task = Task.Run(() => RenewConnectionAsync(connection));
        _connectionRenewalTasks.Add(task);
        _ = task.ContinueWith(completed =>
        {
            lock (_connectionsLock)
                _connectionRenewalTasks.Remove(completed);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task<DateTimeOffset?> GetConnectionExpiryAsync(BoltConnection connection, CancellationToken ct)
    {
        var response = await InvokeOnConnectionAsync(
            connection, string.Empty, BoltConnectionControl.GetExpiry, ReadOnlyMemory<byte>.Empty, ct);
        // Old Hubs do not implement these commands. Leave their existing reconnect behavior intact.
        if (response.StatusCode != HttpStatusCode.OK || response.Data.Length != sizeof(long))
            return null;
        var milliseconds = BinaryPrimitives.ReadInt64LittleEndian(response.Data.Span);
        return milliseconds == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private async Task RenewConnectionAsync(BoltConnection connection)
    {
        using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCts.Token, connection.ReceiveCts!.Token);
        var ct = renewalCts.Token;
        try
        {
            var expiry = await GetConnectionExpiryAsync(connection, ct);
            if (expiry is null)
                return;

            var remaining = expiry.Value - DateTimeOffset.UtcNow;
            var lead = TimeSpan.FromSeconds(Math.Min(
                Math.Max(1, _config.ConnectionRenewalLeadSeconds), Math.Max(0, remaining.TotalSeconds / 2)));
            var delay = remaining - lead - TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * lead.TotalMilliseconds * 0.05);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);

            while (!ct.IsCancellationRequested && connection.IsAvailable && DateTimeOffset.UtcNow < expiry)
            {
                try
                {
                    if (await TryReplaceConnectionAsync(connection, expiry.Value, ct))
                        return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // The still-authenticated old socket remains usable. Never replay its RPCs.
                    _logger.LogWarning("Bolt connection renewal failed ({Failure}); retaining current connection until expiry", ex.GetType().Name);
                }
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning("Bolt connection renewal stopped ({Failure})", ex.GetType().Name);
        }
    }

    private async Task<bool> TryReplaceConnectionAsync(
        BoltConnection previous, DateTimeOffset previousExpiry, CancellationToken ct)
    {
        await _connectionRenewalGate.WaitAsync(ct);
        BoltConnection? replacement = null;
        var installed = false;
        try
        {
            replacement = await CreateConnectionAsync(ct);
            var replacementExpiry = await GetConnectionExpiryAsync(replacement, ct);
            // A provider may return a cached ticket until its refresh window opens.
            if (replacementExpiry is null || replacementExpiry <= previousExpiry)
                return false;

            lock (_connectionsLock)
            {
                if (_disposed || !replacement.IsAvailable || !_connections.Remove(previous))
                    return false;
                _connections.Add(replacement);
                installed = true;
                StartConnectionRenewal(replacement);
            }

            // Publish the working replacement first. A failed drain must not leave the only
            // client socket accepting outbound calls but excluded from the Hub's inbound routes.
            var drain = await InvokeOnConnectionAsync(
                previous, string.Empty, BoltConnectionControl.Drain, ReadOnlyMemory<byte>.Empty, ct);
            if (drain.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException("Hub rejected connection drain.");

            _logger.LogInformation("Bolt connection renewed before expiry; draining existing work without replay");
            await RestoreSubscriptionsAsync(ct);
            RaiseLifecycleEvent(Reconnected);

            return true;
        }
        finally
        {
            _connectionRenewalGate.Release();
            // Keep accepted calls on their original socket even if subscription restoration or
            // the drain acknowledgement fails. Include work already queued by the Hub but not yet
            // received locally: a locally idle socket is not proof that its routes have drained.
            if (installed)
            {
                var drainTime = previousExpiry - DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(250);
                if (drainTime > _rpcTimeout)
                    drainTime = _rpcTimeout;
                try
                {
                    if (drainTime > TimeSpan.Zero)
                        await Task.Delay(drainTime, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            }
            var retired = installed ? previous : replacement;
            if (retired is not null)
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                try { await retired.Transport.CloseAsync(closeCts.Token); } catch { }
                await DiscardConnectionAsync(retired);
            }
        }
    }

    private void FailPendingCallsForConnection(BoltConnection connection)
    {
        foreach (var (requestId, owner) in _pendingCallConnections)
        {
            if (ReferenceEquals(owner, connection) && _pendingCalls.TryRemove(requestId, out var call))
                call.SetException(new InvalidOperationException("Connection lost"));
        }
    }
}
