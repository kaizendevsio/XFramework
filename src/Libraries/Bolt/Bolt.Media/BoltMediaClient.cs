using System.Collections.Concurrent;
using System.Net;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Protocol.Buffers;
using Microsoft.Extensions.Logging;

namespace Bolt.Media;

/// <summary>
/// Media-enabled wrapper over <see cref="BoltClient"/>.
/// Adds voice/video call support: call signaling, media stream management,
/// encryption, ABR, and all media frame handling.
///
/// Usage:
///   var client = new BoltClient(...);
///   var media = new BoltMediaClient(client, logger);
///   await client.ConnectAsync();
///   var callId = await media.StartCallAsync("recipient");
/// </summary>
public sealed class BoltMediaClient : IAsyncDisposable
{
    private readonly BoltClient _client;
    private readonly ILogger _logger;

    /// <summary>The underlying BoltClient for direct transport access.</summary>
    public BoltClient Client => _client;

    private readonly ConcurrentDictionary<Guid, ClientCallInfo> _activeCalls = new();
    private readonly ConcurrentDictionary<Guid, BoltMediaStream> _mediaStreams = new();
    private readonly ConcurrentDictionary<Guid, AdaptiveBitrateController> _bitrateControllers = new();

    // Call events
    public event Func<IncomingCallInfo, Task>? OnIncomingCall;
    public event Func<Guid, Task>? OnCallAnswered;
    public event Func<Guid, string?, Task>? OnCallRejected;
    public event Func<Guid, Task>? OnCallEnded;
    public event Action<Guid>? OnKeyframeRequested;
    public event Action<BoltMediaStream>? OnMediaStreamConfigured;

    /// <summary>Explicit externally authenticated payload mode; configure before connecting.
    /// The factory must return a fail-closed provider before the stream is published to frame handlers.</summary>
    public Func<Guid, string, IMediaEncryption>? AuthenticatedStreamEncryptionFactory { get; set; }

    /// <summary>
    /// The current call-signaling contract does not bind ECDH keys to an authenticated
    /// transport identity, so built-in encrypted calls remain unavailable.
    /// </summary>
    public static bool BuiltInAuthenticatedEncryptionAvailable => false;

    public BoltMediaClient(BoltClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
        _client.Disconnected += HandleDisconnected;

        // Register frame handlers for all media frame types
        RegisterBorrowedFrameHandler(FrameType.MediaFrame, HandleMediaFrame);
        RegisterBorrowedFrameHandler(FrameType.MediaConfig, HandleMediaConfig);
        RegisterBorrowedFrameHandler(FrameType.MediaFeedback, HandleMediaFeedback);
        RegisterBorrowedFrameHandler(FrameType.MediaKeyRequest, HandleMediaKeyRequest);
        RegisterBorrowedFrameHandler(FrameType.FecFrame, HandleFecFrame);
        RegisterBorrowedFrameHandler(FrameType.NackRequest, HandleNackRequest);
        RegisterBorrowedFrameHandler(FrameType.CallSignal, HandleCallSignal);
    }

    private void RegisterBorrowedFrameHandler(
        FrameType frameType,
        Action<BoltConnection, byte[], int> handler) =>
        _client.RegisterFrameHandler(frameType, handler);

    /// <summary>Built-in signaling cannot safely exchange media keys yet.</summary>
    public void SetEncryptionFactory(Func<IMediaEncryption> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        throw new NotSupportedException(
            "Encrypted Bolt Media calls require an identity-bound key exchange, which the current signaling contract does not provide.");
    }

    // ── Call API ─────────────────────────────────────────────

    /// <summary>Initializes only local call state. The authenticated host independently authorizes group admission.</summary>
    public Task JoinHostedGroupAsync(Guid callId)
    {
        if (callId == Guid.Empty) throw new ArgumentException("A host-assigned call ID is required.", nameof(callId));
        if (_client.ServerUri.Scheme != "wss") throw new InvalidOperationException("Hosted media requires a secure WebSocket connection.");
        _client.GetPrimaryConnection(); // Do not create a local active call on an unconnected transport.
        if (!_activeCalls.TryAdd(callId, new ClientCallInfo { CallId = callId, IsOutgoing = true, Status = ClientCallStatus.Active }))
            throw new InvalidOperationException("This call is already active.");
        return Task.CompletedTask;
    }

    public async Task<Guid> StartCallAsync(string recipientId, bool video = false, bool encrypted = false, Guid? authorizedCallId = null)
    {
        if (encrypted)
            throw new NotSupportedException(
                "Encrypted Bolt Media calls are disabled until key exchange is bound to authenticated peer identities.");

        var callId = authorizedCallId ?? Guid.NewGuid();
        if (callId == Guid.Empty)
            throw new ArgumentException("The authorized call ID cannot be empty.", nameof(authorizedCallId));
        if (!_activeCalls.TryAdd(callId, new ClientCallInfo { CallId = callId, IsOutgoing = true, RemoteClientId = recipientId }))
            throw new InvalidOperationException("This call is already active.");

        var recipientHash = BoltCodec.Fnv1aHash(recipientId);
        var payload = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload, recipientHash);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Initiate, payload);
        try
        {
            await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        }
        catch
        {
            _activeCalls.TryRemove(callId, out _);
            throw;
        }

        return callId;
    }

    public async Task AnswerCallAsync(Guid callId, bool encrypted = false)
    {
        if (encrypted)
            throw new NotSupportedException(
                "Encrypted Bolt Media calls are disabled until key exchange is bound to authenticated peer identities.");

        _activeCalls.TryGetValue(callId, out var call);
        var previousStatus = call?.Status;
        if (call is not null)
            call.Status = ClientCallStatus.Active;

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Answer, ReadOnlySpan<byte>.Empty);
        try
        {
            await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        }
        catch
        {
            if (call is not null && previousStatus is { } status)
                call.Status = status;
            throw;
        }
    }

    public async Task RejectCallAsync(Guid callId)
    {
        _activeCalls.TryRemove(callId, out _);
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Reject, ReadOnlySpan<byte>.Empty);
        try
        {
            await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        }
        finally
        {
            await CleanupCallStreamsAsync(callId);
        }
    }

    public async Task EndCallAsync(Guid callId)
    {
        _activeCalls.TryRemove(callId, out _);
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.End, ReadOnlySpan<byte>.Empty);
        try
        {
            await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        }
        finally
        {
            await CleanupCallStreamsAsync(callId);
        }
    }

    public BoltMediaStream? GetMediaStream(Guid streamId)
        => _mediaStreams.TryGetValue(streamId, out var stream) ? stream : null;

    /// <summary>Ask a remote sender for a keyframe. The relay routes it back to that stream's owner.</summary>
    public async ValueTask RequestRemoteKeyframeAsync(Guid streamId, CancellationToken ct = default)
    {
        if (!_mediaStreams.ContainsKey(streamId)) return;
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaKeyRequest(writer, streamId);
        try { await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, ct); }
        catch (Exception ex) { _logger.LogDebug(ex, "Keyframe request for {StreamId} was not delivered", streamId); }
    }

    public bool RegisterMediaStream(BoltMediaStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return _activeCalls.ContainsKey(stream.CallId) && _mediaStreams.TryAdd(stream.StreamId, stream);
    }

    /// <summary>Attach feedback to the local video stream, not just the receiver's stream copy.</summary>
    public void ConfigureVideoFeedback(Guid streamId, int bitrateKbps)
    {
        if (!_mediaStreams.TryGetValue(streamId, out var stream) || stream.IsAudio) return;
        if (_bitrateControllers.TryGetValue(streamId, out var existing))
        { existing.SetVideoTarget(bitrateKbps); return; }
        var controller = new AdaptiveBitrateController(_client.GetPrimaryConnection(), streamId, bitrateKbps, false);
        controller.OnBitrateChanged += stream.RaiseBitrateChanged;
        controller.OnKeyframeRequested += stream.RaiseKeyframeNeeded;
        _bitrateControllers[streamId] = controller;
        // Local streams consume feedback; only remote streams start the reporting timer.
    }

    public async Task<BoltMediaStream> SendScreenShareConfigAsync(Guid callId, int width = 1920, int height = 1080, int bitrateKbps = 3000, CancellationToken ct = default)
    {
        var streamId = Guid.NewGuid();
        var conn = _client.GetPrimaryConnection();

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaConfig(writer, streamId, callId, MediaType.ScreenShare, CodecId.H264,
            width, height, bitrateKbps, 0, ReadOnlySpan<byte>.Empty);
        await conn.SendAsync(writer.WrittenMemory, ct);

        var stream = new BoltMediaStream(conn, streamId, callId, false);
        stream.EnableNack(256);
        if (!RegisterMediaStream(stream))
        {
            await stream.DisposeAsync();
            throw new InvalidOperationException("Cannot register a media stream for an inactive call.");
        }

        return stream;
    }

    // ── Frame handlers (registered with BoltClient) ──────────

    private void HandleMediaFrame(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadMediaFrame(buffer.AsSpan(0, length), out var header)) return;
        if (AuthenticatedStreamEncryptionFactory is not null && header.PayloadLength > 5155) return;
        if (_mediaStreams.TryGetValue(header.StreamId, out var stream))
        {
            var payload = header.GetPayload(buffer.AsSpan(0, length)).ToArray();
            _ = stream.EnqueueFrameAsync(header.SequenceNumber, header.Timestamp, payload, header.Flags);

            if (_bitrateControllers.TryGetValue(header.StreamId, out var controller))
                controller.RecordFrameReceived(header.SequenceNumber, header.Timestamp);
        }
    }

    private void HandleMediaConfig(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadMediaConfig(buffer.AsSpan(0, length), out var config)) return;
        if (!_activeCalls.ContainsKey(config.CallId) || _mediaStreams.ContainsKey(config.StreamId)) return;

        var isAudio = config.MediaType == MediaType.Audio;
        var stream = new BoltMediaStream(conn, config.StreamId, config.CallId, isAudio);
        stream.Codec = config.CodecId;
        stream.Width = config.Param1;
        stream.Height = config.Param2;
        if (AuthenticatedStreamEncryptionFactory is { } encryptionFactory)
        {
            // Eight participants may each publish voice and camera, so sixteen routes is the ceiling.
            var codecAllowed = isAudio ? config.CodecId == CodecId.Opus
                : config.MediaType == MediaType.Video && config.CodecId is CodecId.H264 or CodecId.VP9 or CodecId.AV1;
            if (_mediaStreams.Count >= 16 || !codecAllowed || (config.Flags & 0x10) == 0 ||
                config.ExtensionLength is < 6 or > 133) return;
            var owner = System.Text.Encoding.UTF8.GetString(buffer, config.ExtensionOffset, config.ExtensionLength);
            if (!owner.StartsWith("SFR1:", StringComparison.Ordinal)) return;
            try { stream.SetEncryption(encryptionFactory(config.CallId, owner[5..])); }
            catch { return; }
            stream.SenderId = owner[5..];
        }
        if (!_mediaStreams.TryAdd(config.StreamId, stream)) return;

        if (AuthenticatedStreamEncryptionFactory is null) stream.EnableFec(isAudio ? 4 : 8);
        stream.EnableNack(isAudio ? 128 : 256);
        stream.EnableDelayBasedControl(config.BitrateKbps);

        // VAD/PLC primitives operate on decoded PCM, not compressed Opus packets.
        // Encoded frames must reach the codec unchanged.

        var controller = new AdaptiveBitrateController(conn, config.StreamId, config.BitrateKbps, isAudio);
        _bitrateControllers[config.StreamId] = controller;
        controller.Start();
        controller.OnBitrateChanged += kbps => stream.RaiseBitrateChanged(kbps);
        controller.OnKeyframeRequested += () => stream.RaiseKeyframeNeeded();

        if (!isAudio && AuthenticatedStreamEncryptionFactory is null) stream.EnableBandwidthProbing(config.BitrateKbps);

        if (_activeCalls.TryGetValue(config.CallId, out var call))
        {
            if (isAudio) call.AudioStreamId = config.StreamId;
            else call.VideoStreamId = config.StreamId;
        }

        OnMediaStreamConfigured?.Invoke(stream);
    }

    private void HandleMediaFeedback(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadMediaFeedback(buffer.AsSpan(0, length), out var feedback)) return;
        if (_bitrateControllers.TryGetValue(feedback.StreamId, out var controller))
            controller.ProcessFeedback(feedback);
    }

    private void HandleMediaKeyRequest(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadMediaKeyRequest(buffer.AsSpan(0, length), out var streamId)) return;
        OnKeyframeRequested?.Invoke(streamId);
    }

    private void HandleFecFrame(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadFecFrame(buffer.AsSpan(0, length), out var header)) return;
        if (_mediaStreams.TryGetValue(header.StreamId, out var stream))
        {
            var payload = header.GetPayload(buffer.AsSpan(0, length)).ToArray();
            _ = stream.EnqueueFecFrameAsync(header.FecGroupStart, header.FecGroupSize, payload);
        }
    }

    private void HandleNackRequest(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadNackRequest(buffer.AsSpan(0, length), out var header)) return;
        if (_mediaStreams.TryGetValue(header.StreamId, out var stream))
        {
            var missingSeqs = header.GetMissingSequences(buffer.AsSpan(0, length))
                .Distinct()
                .Take(64)
                .ToArray();
            _ = stream.HandleNackAsync(missingSeqs);
        }
    }

    private void HandleCallSignal(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadCallSignal(buffer.AsSpan(0, length), out var header)) return;
        if (header.SignalType == SignalType.StreamEnded)
        {
            if (header.PayloadLength == 16 && _activeCalls.ContainsKey(header.CallId))
                _ = RemoveRemoteStreamAsync(header.CallId, new Guid(buffer.AsSpan(header.PayloadOffset, 16)));
            return;
        }
        _ = HandleCallSignalAsync(header);
    }

    private async Task RemoveRemoteStreamAsync(Guid callId, Guid streamId)
    {
        if (!_mediaStreams.TryGetValue(streamId, out var stream) || stream.CallId != callId) return;
        if (!_mediaStreams.TryRemove(new KeyValuePair<Guid, BoltMediaStream>(streamId, stream))) return;
        _bitrateControllers.TryRemove(streamId, out var controller);
        try { await stream.DisposeAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose ended media stream {StreamId}", streamId); }
        if (controller is not null)
        {
            try { await controller.DisposeAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose ended bitrate controller {StreamId}", streamId); }
        }
    }

    private async Task HandleCallSignalAsync(CallSignalHeader header)
    {
        switch (header.SignalType)
        {
            case SignalType.Initiate:
                _activeCalls[header.CallId] = new ClientCallInfo { CallId = header.CallId, IsOutgoing = false, Status = ClientCallStatus.Ringing };
                if (OnIncomingCall != null) await OnIncomingCall(new IncomingCallInfo(header.CallId, "", false));
                break;
            case SignalType.Ring:
                if (_activeCalls.TryGetValue(header.CallId, out var ringing)) ringing.Status = ClientCallStatus.Ringing;
                break;
            case SignalType.Answer:
                if (_activeCalls.TryGetValue(header.CallId, out var answered)) answered.Status = ClientCallStatus.Active;
                if (OnCallAnswered != null) await OnCallAnswered(header.CallId);
                break;
            case SignalType.Reject:
                _activeCalls.TryRemove(header.CallId, out _);
                await CleanupCallStreamsAsync(header.CallId);
                if (OnCallRejected != null) await OnCallRejected(header.CallId, null);
                break;
            case SignalType.End:
                _activeCalls.TryRemove(header.CallId, out _);
                await CleanupCallStreamsAsync(header.CallId);
                if (OnCallEnded != null) await OnCallEnded(header.CallId);
                break;
            case SignalType.KeyExchange:
                _logger.LogWarning(
                    "Ignoring unauthenticated Bolt Media key exchange for call {CallId}; encrypted media remains disabled",
                    header.CallId);
                break;
        }
    }

    private async Task CleanupCallStreamsAsync(Guid callId)
    {
        foreach (var (streamId, stream) in _mediaStreams)
        {
            if (stream.CallId != callId)
                continue;

            _mediaStreams.TryRemove(streamId, out _);
            _bitrateControllers.TryRemove(streamId, out var controller);
            try { await stream.DisposeAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose media stream {StreamId}", streamId); }
            if (controller is not null)
            {
                try { await controller.DisposeAsync(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose bitrate controller {StreamId}", streamId); }
            }
        }
    }

    private void HandleDisconnected() => _ = EndDisconnectedCallsAsync();

    private async Task EndDisconnectedCallsAsync()
    {
        foreach (var callId in _activeCalls.Keys)
        {
            if (!_activeCalls.TryRemove(callId, out _))
                continue;
            await CleanupCallStreamsAsync(callId);
            try
            {
                if (OnCallEnded is not null)
                    await OnCallEnded(callId);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Disconnected media call cleanup callback failed"); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Disconnected -= HandleDisconnected;
        _client.UnregisterFrameHandler(FrameType.MediaFrame, HandleMediaFrame);
        _client.UnregisterFrameHandler(FrameType.MediaConfig, HandleMediaConfig);
        _client.UnregisterFrameHandler(FrameType.MediaFeedback, HandleMediaFeedback);
        _client.UnregisterFrameHandler(FrameType.MediaKeyRequest, HandleMediaKeyRequest);
        _client.UnregisterFrameHandler(FrameType.FecFrame, HandleFecFrame);
        _client.UnregisterFrameHandler(FrameType.NackRequest, HandleNackRequest);
        _client.UnregisterFrameHandler(FrameType.CallSignal, HandleCallSignal);

        foreach (var (streamId, stream) in _mediaStreams)
        {
            try { await stream.DisposeAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose media stream {StreamId}", streamId); }
        }
        _mediaStreams.Clear();
        foreach (var (streamId, controller) in _bitrateControllers)
        {
            try { await controller.DisposeAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose bitrate controller {StreamId}", streamId); }
        }
        _bitrateControllers.Clear();
        _activeCalls.Clear();
    }
}

/// <summary>Information about an incoming call.</summary>
public record IncomingCallInfo(Guid CallId, string CallerClientId, bool VideoRequested);

internal enum ClientCallStatus { Initiating, Ringing, Active, Held, Ended }

internal sealed class ClientCallInfo
{
    public Guid CallId { get; init; }
    public bool IsOutgoing { get; init; }
    public string RemoteClientId { get; set; } = "";
    public ClientCallStatus Status { get; set; } = ClientCallStatus.Initiating;
    public Guid? AudioStreamId { get; set; }
    public Guid? VideoStreamId { get; set; }
}
