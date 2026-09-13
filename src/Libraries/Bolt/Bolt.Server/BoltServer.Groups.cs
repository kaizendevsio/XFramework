using System.Buffers;
using Bolt.Protocol;

namespace Bolt.Server;

public sealed partial class BoltServer
{
    private readonly IBoltGroupCallAuthorizer? _groupCallAuthorizer;

    /// <summary>Notifies the host after a participant leaves or loses authorization.</summary>
    public event Action<Guid, string>? GroupParticipantRemoved;

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
            await RenewGroupAuthorizationAsync(call, ct, force: true);
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
            }
            call.LastMediaAuthorizationTick = Environment.TickCount64;
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
            if (participant is not null) await RemoveGroupParticipantCoreAsync(call, participant, ct);
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

    // GroupGate is held by the frame/config/admission path, including the authorization awaits.
    private async Task<bool> RenewGroupAuthorizationAsync(ServerCallState call, CancellationToken ct, bool force = false)
    {
        if (!force && Environment.TickCount64 - call.LastMediaAuthorizationTick < 5000) return IsCallMediaActive(call);
        // At most eight independent policy checks; do not serialize remote directory lookups
        // and stall the audio fanout for the sum of every participant's round-trip time.
        var results = await Task.WhenAll(GetParticipantSnapshot(call).Select(async participant =>
            (Participant: participant, Allowed: participant.IsAlive && await AuthorizeGroupParticipantAsync(call.CallId, participant, ct))));
        foreach (var result in results)
            if (!result.Allowed) await RemoveGroupParticipantCoreAsync(call, result.Participant, ct);
        call.LastMediaAuthorizationTick = Environment.TickCount64;
        return IsCallMediaActive(call);
    }

    private async Task RemoveGroupParticipantCoreAsync(ServerCallState call, BoltHubConnection participant, CancellationToken ct)
    {
        var removedStreams = new List<Guid>();
        lock (call.SyncRoot)
        {
            lock (call.Participants) call.Participants.RemoveAll(x => x.StreamId == participant.StreamId);
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
        try { if (participant.ClientId is { } clientId) GroupParticipantRemoved?.Invoke(call.CallId, clientId); }
        catch { /* Host notifications do not restore removed media access. */ }
    }

    private void CleanupEmptyGroupCall(ServerCallState call)
    {
        if (GetParticipantSnapshot(call).Count == 0 &&
            _activeCalls.TryRemove(new KeyValuePair<Guid, ServerCallState>(call.CallId, call)))
            call.Status = ServerCallStatus.Ended;
    }
}
