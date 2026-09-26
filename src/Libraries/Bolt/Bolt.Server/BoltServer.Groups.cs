using System.Buffers;
using Bolt.Protocol;
using Microsoft.Extensions.Logging;

namespace Bolt.Server;

public sealed partial class BoltServer
{
    private readonly IBoltGroupCallAuthorizer? _groupCallAuthorizer;

    /// <summary>Notifies the host after a participant leaves or loses authorization.</summary>
    public event Action<Guid, string>? GroupParticipantRemoved;

    /// <summary>
    /// The same notification with its reason. A host that holds seats across reconnects must end the
    /// seat only for <see cref="BoltGroupDepartureReason.Unauthorized"/> or an explicit leave, never
    /// because a socket closed.
    /// </summary>
    public event Action<BoltGroupDeparture>? GroupParticipantDeparted;

    /// <summary>
    /// Host-only admission after explicit user acceptance and authenticated transport registration.
    /// This lifecycle API supplies no encryption keys and is disabled without a group authorization policy.
    /// </summary>
    public async Task<bool> JoinGroupCallAsync(Guid callId, string clientId, CancellationToken ct = default)
    {
        if (!_mediaEnabled || _groupCallAuthorizer is null || callId == Guid.Empty || string.IsNullOrEmpty(clientId)) return false;
        var matches = _connectionsByStreamId.Values.Where(x => x.ClientId == clientId && x.IsAlive).Take(2).ToArray();
        if (matches.Length != 1) return false;
        var connection = matches[0];
        if (connection is null || !await AuthorizeGroupParticipantAsync(callId, connection, ct)) return false;
        ServerCallState call;
        lock (_callAdmissionLock)
        {
            if (!_activeCalls.TryGetValue(callId, out call!))
            {
                if (_activeCalls.Count >= _maxActiveCalls) return false;
                call = new ServerCallState { CallId = callId, HostManagedGroup = true,
                    CallerConnection = connection, Status = ServerCallStatus.Active };
                if (!_activeCalls.TryAdd(callId, call)) return false;
            }
        }
        if (!call.HostManagedGroup) return false;
        var entered = false;
        try
        {
            await call.GroupGate.WaitAsync(ct);
            entered = true;
            if (!_activeCalls.TryGetValue(callId, out var current) || !ReferenceEquals(call, current) ||
                !connection.IsAlive || !await AuthorizeGroupParticipantAsync(callId, connection, ct)) return false;
            await RenewGroupAuthorizationAsync(call, ct);
            if (IsCallParticipant(call, connection)) return true;
            if (GetParticipantSnapshot(call).Count >= Math.Min(8, _maxCallParticipants) ||
                _activeCalls.Values.Count(x => GetParticipantSnapshot(x).Any(p => p.QuotaKey == connection.QuotaKey)) >= _maxActiveCallsPerPrincipal)
                return false;

            // Queue every existing configuration before adding the recipient to live frame routes.
            foreach (var id in GetMediaStreamSnapshot(call))
                if (_activeMediaStreams.TryGetValue(id, out var stream) && stream.Configuration is { } config)
                    await connection.SendAsync(config, ct);
            if (!connection.IsAlive || !await AuthorizeGroupParticipantAsync(callId, connection, ct)) return false;
            lock (_callAdmissionLock)
            lock (call.SyncRoot)
            {
                if (!IsCallMediaActive(call)) return false;
                if (_activeCalls.Values.Count(x => GetParticipantSnapshot(x).Any(p => p.QuotaKey == connection.QuotaKey)) >= _maxActiveCallsPerPrincipal ||
                    !TryAddCallParticipant(call, connection)) return false;
                foreach (var id in GetMediaStreamSnapshot(call))
                    if (_activeMediaStreams.TryGetValue(id, out var stream)) stream.AddRecipient(connection);
                call.ParticipantAuthorizedAt[connection.StreamId] = Environment.TickCount64;
            }
            return true;
        }
        finally
        {
            // A cancelled first admission must not retain an empty Active room. If another admission
            // owns the gate, it owns cleanup; never delete its state while it is awaiting authorization.
            if (entered || call.GroupGate.Wait(0))
            {
                CleanupEmptyGroupCall(call);
                call.GroupGate.Release();
            }
        }
    }

    /// <summary>Host removal, or a peer's own End signal. Remaining participants keep the room.</summary>
    public async Task LeaveGroupCallAsync(Guid callId, string? clientId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(clientId)) return;
        if (!_activeCalls.TryGetValue(callId, out var call) || !call.HostManagedGroup) return;
        await call.GroupGate.WaitAsync(ct);
        try
        {
            var participant = GetParticipantSnapshot(call).FirstOrDefault(x => x.ClientId == clientId);
            if (participant is not null) await RemoveGroupParticipantCoreAsync(call, participant, ct, BoltGroupDepartureReason.Left);
        }
        finally { call.GroupGate.Release(); }
    }

    /// <summary>
    /// Remove exactly this connection, never "whoever has its client ID": after a resume the same
    /// participant is already back on a newer connection, and the old one's cleanup must not evict it.
    /// </summary>
    private async Task LeaveGroupCallAsync(Guid callId, BoltHubConnection connection, BoltGroupDepartureReason reason, CancellationToken ct)
    {
        if (!_activeCalls.TryGetValue(callId, out var call) || !call.HostManagedGroup) return;
        await call.GroupGate.WaitAsync(ct);
        try
        {
            if (IsCallParticipant(call, connection)) await RemoveGroupParticipantCoreAsync(call, connection, ct, reason);
        }
        finally { call.GroupGate.Release(); }
    }

    private async ValueTask<bool> AuthorizeGroupParticipantAsync(Guid callId, BoltHubConnection participant, CancellationToken ct)
    {
        if (_groupCallAuthorizer is null || participant.User?.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(participant.ClientId)) return false;
        try { return await _groupCallAuthorizer.AuthorizeParticipantAsync(callId, participant.ClientId, participant.User, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return false; }
    }

    /// <summary>
    /// Admission path only (GroupGate held): re-check every participant now and apply the result.
    /// The media path never waits for this; it uses <see cref="ScheduleGroupAuthorizationRenewal"/>.
    /// </summary>
    private async Task<bool> RenewGroupAuthorizationAsync(ServerCallState call, CancellationToken ct)
    {
        var decisions = await CheckGroupParticipantsAsync(call, ct);
        await ApplyGroupAuthorizationAsync(call, decisions, ct);
        Volatile.Write(ref call.LastMediaAuthorizationTick, Environment.TickCount64);
        return IsCallMediaActive(call);
    }

    /// <summary>
    /// Media and configuration paths: start a background re-check when the lease is due. Frames keep
    /// flowing on the last decisions meanwhile; only the removal itself takes the group gate.
    /// </summary>
    private void ScheduleGroupAuthorizationRenewal(ServerCallState call)
    {
        if (Environment.TickCount64 - Volatile.Read(ref call.LastMediaAuthorizationTick) < _groupAuthorizationRenewalIntervalMs ||
            Interlocked.CompareExchange(ref call.AuthorizationRenewalRunning, 1, 0) != 0)
            return;
        _ = Task.Run(() => RunGroupAuthorizationRenewalAsync(call));
    }

    private async Task RunGroupAuthorizationRenewalAsync(ServerCallState call)
    {
        try
        {
            using var checkCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
            // A policy call that never answers is "unavailable", not a reason to stall renewal forever.
            checkCts.CancelAfter(_invocationTimeoutMs);
            var decisions = await CheckGroupParticipantsAsync(call, checkCts.Token);
            await call.GroupGate.WaitAsync(_shutdownCts.Token);
            try { await ApplyGroupAuthorizationAsync(call, decisions, _shutdownCts.Token); }
            finally { call.GroupGate.Release(); }
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposed) != 0) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Group authorization renewal failed for call {CallId}; participants keep their last decision", call.CallId);
        }
        finally
        {
            Volatile.Write(ref call.LastMediaAuthorizationTick, Environment.TickCount64);
            Volatile.Write(ref call.AuthorizationRenewalRunning, 0);
        }
    }

    // At most eight independent policy checks, run concurrently and without the group gate.
    private async Task<(BoltHubConnection Participant, BoltGroupAuthorizationDecision Decision)[]> CheckGroupParticipantsAsync(
        ServerCallState call, CancellationToken ct) =>
        await Task.WhenAll(GetParticipantSnapshot(call).Select(async participant =>
            (participant, await RenewGroupParticipantAsync(call.CallId, participant, ct))));

    private async Task<BoltGroupAuthorizationDecision> RenewGroupParticipantAsync(
        Guid callId, BoltHubConnection participant, CancellationToken ct)
    {
        if (_groupCallAuthorizer is null || !participant.IsAlive ||
            participant.User?.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(participant.ClientId))
            return BoltGroupAuthorizationDecision.Denied;
        try { return await _groupCallAuthorizer.RenewParticipantAsync(callId, participant.ClientId, participant.User, ct); }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested) { throw; }
        // A timeout or an unreachable policy backend cannot prove a revocation.
        catch { return BoltGroupAuthorizationDecision.Unavailable; }
    }

    private async Task ApplyGroupAuthorizationAsync(
        ServerCallState call,
        (BoltHubConnection Participant, BoltGroupAuthorizationDecision Decision)[] decisions,
        CancellationToken ct)
    {
        var now = Environment.TickCount64;
        foreach (var (participant, decision) in decisions)
        {
            if (!IsCallParticipant(call, participant)) continue;
            switch (decision)
            {
                case BoltGroupAuthorizationDecision.Allowed:
                    call.ParticipantAuthorizedAt[participant.StreamId] = now;
                    break;
                case BoltGroupAuthorizationDecision.Unavailable when
                    now - call.ParticipantAuthorizedAt.GetValueOrDefault(participant.StreamId, now) < _groupAuthorizationGraceMs:
                    BoltServerMetrics.RecordGroupAuthorizationUnavailable();
                    _logger.LogWarning(
                        "Group authorization for {ClientId} in call {CallId} is temporarily unavailable; keeping the participant",
                        participant.ClientId, call.CallId);
                    break;
                default:
                    _logger.LogInformation(
                        "Removing {ClientId} from call {CallId}: authorization {Decision}",
                        participant.ClientId, call.CallId, decision == BoltGroupAuthorizationDecision.Denied ? "refused" : "unavailable beyond grace");
                    await RemoveGroupParticipantCoreAsync(call, participant, ct, BoltGroupDepartureReason.Unauthorized);
                    break;
            }
        }
    }

    private async Task RemoveGroupParticipantCoreAsync(ServerCallState call, BoltHubConnection participant, CancellationToken ct,
        BoltGroupDepartureReason reason)
    {
        var removedStreams = new List<Guid>();
        lock (call.SyncRoot)
        {
            lock (call.Participants) call.Participants.RemoveAll(x => x.StreamId == participant.StreamId);
            call.ParticipantAuthorizedAt.TryRemove(participant.StreamId, out _);
            call.RecipientPreferredLayer.TryRemove(participant.StreamId, out _);
            call.SimulcastGroups.TryRemove(participant.StreamId, out _);
            foreach (var id in GetMediaStreamSnapshot(call))
            {
                if (!_activeMediaStreams.TryGetValue(id, out var route)) continue;
                route.RemoveRecipientsWhere(x => x.StreamId == participant.StreamId);
                if (route.Sender.StreamId != participant.StreamId || !_activeMediaStreams.TryRemove(id, out _)) continue;
                ReleaseQuota(_activeMediaStreamsByPrincipal, participant.QuotaKey);
                RemoveMediaStream(call, id);
                removedStreams.Add(id);
            }
            CleanupEmptyGroupCall(call);
        }
        // State is removed before notifications; a slow or disconnected recipient cannot retain access.
        foreach (var streamId in removedStreams)
        {
            var writer = new ArrayBufferWriter<byte>();
            BoltCodec.WriteCallSignal(writer, call.CallId, SignalType.StreamEnded, streamId.ToByteArray());
            foreach (var remaining in GetParticipantSnapshot(call))
                if (remaining.IsAlive)
                {
                    try { await remaining.SendAsync(writer.WrittenMemory, ct); }
                    catch { /* A failed notification cannot interrupt the already committed removal. */ }
                }
        }
        if (participant.ClientId is { } clientId)
        {
            try { GroupParticipantRemoved?.Invoke(call.CallId, clientId); }
            catch { /* Host notifications do not restore removed media access. */ }
            try { GroupParticipantDeparted?.Invoke(new BoltGroupDeparture(call.CallId, clientId, reason, participant.User)); }
            catch { /* Same. */ }
        }
    }

    private void CleanupEmptyGroupCall(ServerCallState call)
    {
        if (GetParticipantSnapshot(call).Count == 0 &&
            _activeCalls.TryRemove(new KeyValuePair<Guid, ServerCallState>(call.CallId, call)))
            call.Status = ServerCallStatus.Ended;
    }
}
