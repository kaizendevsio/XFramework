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

    /// <summary>
    /// The current call-signaling contract does not bind ECDH keys to an authenticated
    /// transport identity, so built-in encrypted calls remain unavailable.
    /// </summary>
    public static bool BuiltInAuthenticatedEncryptionAvailable => false;

    public BoltMediaClient(BoltClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;

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

    public async Task<Guid> StartCallAsync(string recipientId, bool video = false, bool encrypted = false)
    {
        if (encrypted)
            throw new NotSupportedException(
                "Encrypted Bolt Media calls are disabled until key exchange is bound to authenticated peer identities.");

        var callId = Guid.NewGuid();
        _activeCalls[callId] = new ClientCallInfo { CallId = callId, IsOutgoing = true, RemoteClientId = recipientId };

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

    public bool RegisterMediaStream(BoltMediaStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return _activeCalls.ContainsKey(stream.CallId) && _mediaStreams.TryAdd(stream.StreamId, stream);
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
        if (_mediaStreams.TryGetValue(header.StreamId, out var stream))
        {
            var payload = header.GetPayload(buffer.AsSpan(0, length)).ToArray();
            _ = stream.EnqueueFrameAsync(header.SequenceNumber, header.Timestamp, payload, header.Flags);

            if (_bitrateControllers.TryGetValue(header.StreamId, out var controller))
                controller.RecordFrameReceived(header.SequenceNumber);
        }
    }

    private void HandleMediaConfig(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadMediaConfig(buffer.AsSpan(0, length), out var config)) return;
        if (!_activeCalls.ContainsKey(config.CallId) || _mediaStreams.ContainsKey(config.StreamId)) return;

        var isAudio = config.MediaType == MediaType.Audio;
        var stream = new BoltMediaStream(conn, config.StreamId, config.CallId, isAudio);
        if (!_mediaStreams.TryAdd(config.StreamId, stream)) return;

        stream.EnableFec(isAudio ? 4 : 8);
        stream.EnableNack(isAudio ? 128 : 256);
        stream.EnableDelayBasedControl(config.BitrateKbps);

        if (isAudio) { stream.EnableVad(); stream.EnablePlc(); }

        var controller = new AdaptiveBitrateController(conn, config.StreamId, config.BitrateKbps, isAudio);
        _bitrateControllers[config.StreamId] = controller;
        controller.Start();
        controller.OnBitrateChanged += kbps => stream.RaiseBitrateChanged(kbps);
        controller.OnKeyframeRequested += () => stream.RaiseKeyframeNeeded();

        if (!isAudio) stream.EnableBandwidthProbing(config.BitrateKbps);

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
        _ = HandleCallSignalAsync(header);
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

    public async ValueTask DisposeAsync()
    {
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
