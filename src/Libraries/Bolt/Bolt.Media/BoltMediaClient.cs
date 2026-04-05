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
    private readonly ConcurrentDictionary<Guid, IMediaEncryption> _callEncryption = new();

    private Func<IMediaEncryption>? _encryptionFactory;

    // Call events
    public event Func<IncomingCallInfo, Task>? OnIncomingCall;
    public event Func<Guid, Task>? OnCallAnswered;
    public event Func<Guid, string?, Task>? OnCallRejected;
    public event Func<Guid, Task>? OnCallEnded;
    public event Action<Guid>? OnKeyframeRequested;

    public BoltMediaClient(BoltClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;

        // Register frame handlers for all media frame types
        client.RegisterFrameHandler(FrameType.MediaFrame, HandleMediaFrame);
        client.RegisterFrameHandler(FrameType.MediaConfig, HandleMediaConfig);
        client.RegisterFrameHandler(FrameType.MediaFeedback, HandleMediaFeedback);
        client.RegisterFrameHandler(FrameType.MediaKeyRequest, HandleMediaKeyRequest);
        client.RegisterFrameHandler(FrameType.FecFrame, HandleFecFrame);
        client.RegisterFrameHandler(FrameType.NackRequest, HandleNackRequest);
        client.RegisterFrameHandler(FrameType.CallSignal, HandleCallSignal);
    }

    /// <summary>Set a custom encryption factory for Blazor WASM (ExternalMediaEncryption).</summary>
    public void SetEncryptionFactory(Func<IMediaEncryption> factory) => _encryptionFactory = factory;

    private IMediaEncryption CreateEncryption() => _encryptionFactory?.Invoke() ?? new MediaEncryption();

    // ── Call API ─────────────────────────────────────────────

    public async Task<Guid> StartCallAsync(string recipientId, bool video = false, bool encrypted = true)
    {
        var callId = Guid.NewGuid();
        _activeCalls[callId] = new ClientCallInfo { CallId = callId, IsOutgoing = true, RemoteClientId = recipientId };

        if (encrypted)
            _callEncryption[callId] = CreateEncryption();

        var recipientHash = BoltCodec.Fnv1aHash(recipientId);
        var payload = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload, recipientHash);

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Initiate, payload);
        await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();

        return callId;
    }

    public async Task AnswerCallAsync(Guid callId)
    {
        if (_activeCalls.TryGetValue(callId, out var call))
            call.Status = ClientCallStatus.Active;

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Answer, ReadOnlySpan<byte>.Empty);
        await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();
    }

    public async Task RejectCallAsync(Guid callId)
    {
        _activeCalls.TryRemove(callId, out _);
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.Reject, ReadOnlySpan<byte>.Empty);
        await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();
        await CleanupCallStreamsAsync(callId);
    }

    public async Task EndCallAsync(Guid callId)
    {
        _activeCalls.TryRemove(callId, out _);
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.End, ReadOnlySpan<byte>.Empty);
        await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();
        await CleanupCallStreamsAsync(callId);
    }

    public BoltMediaStream? GetMediaStream(Guid streamId)
        => _mediaStreams.TryGetValue(streamId, out var stream) ? stream : null;

    public async Task<BoltMediaStream> SendScreenShareConfigAsync(Guid callId, int width = 1920, int height = 1080, int bitrateKbps = 3000, CancellationToken ct = default)
    {
        var streamId = Guid.NewGuid();
        var conn = _client.GetPrimaryConnection();

        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaConfig(writer, streamId, callId, MediaType.ScreenShare, CodecId.H264,
            width, height, bitrateKbps, 0, ReadOnlySpan<byte>.Empty);
        await conn.SendAsync(writer.WrittenMemory, ct);
        writer.Reset();

        var stream = new BoltMediaStream(conn, streamId, callId, false);
        stream.EnableNack(256);
        if (_callEncryption.TryGetValue(callId, out var enc) && enc.IsReady)
            stream.SetEncryption(enc);
        _mediaStreams[streamId] = stream;

        return stream;
    }

    // ── Frame handlers (registered with BoltClient) ──────────

    private void HandleMediaFrame(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadMediaFrame(buffer.AsSpan(0, length), out var header)) return;
        if (_mediaStreams.TryGetValue(header.StreamId, out var stream))
        {
            var payload = header.GetPayload(buffer.AsSpan(0, length)).ToArray();
            stream.EnqueueFrame(header.SequenceNumber, header.Timestamp, payload, header.Flags);

            if (_bitrateControllers.TryGetValue(header.StreamId, out var controller))
                controller.RecordFrameReceived(header.SequenceNumber);
        }
    }

    private void HandleMediaConfig(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadMediaConfig(buffer.AsSpan(0, length), out var config)) return;

        var isAudio = config.MediaType == MediaType.Audio;
        var stream = new BoltMediaStream(conn, config.StreamId, config.CallId, isAudio);
        _mediaStreams[config.StreamId] = stream;

        stream.EnableNack(isAudio ? 128 : 256);
        stream.EnableDelayBasedControl(config.BitrateKbps);

        if (isAudio) { stream.EnableVad(); stream.EnablePlc(); }

        if (_callEncryption.TryGetValue(config.CallId, out var enc) && enc.IsReady)
            stream.SetEncryption(enc);

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
            stream.EnqueueFecFrame(header.FecGroupStart, header.FecGroupSize, payload);
        }
    }

    private void HandleNackRequest(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadNackRequest(buffer.AsSpan(0, length), out var header)) return;
        if (_mediaStreams.TryGetValue(header.StreamId, out var stream))
        {
            var missingSeqs = header.GetMissingSequences(buffer.AsSpan(0, length));
            _ = stream.HandleNackAsync(missingSeqs);
        }
    }

    private void HandleCallSignal(BoltConnection conn, byte[] buffer, int length)
    {
        if (!BoltCodec.TryReadCallSignal(buffer.AsSpan(0, length), out var header)) return;
        _ = HandleCallSignalAsync(header, buffer, length);
    }

    private async Task HandleCallSignalAsync(CallSignalHeader header, byte[] data, int length)
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
                await SendKeyExchangeAsync(header.CallId);
                if (OnCallAnswered != null) await OnCallAnswered(header.CallId);
                break;
            case SignalType.Reject:
                _activeCalls.TryRemove(header.CallId, out _);
                CleanupCallEncryption(header.CallId);
                if (OnCallRejected != null) await OnCallRejected(header.CallId, null);
                break;
            case SignalType.End:
                _activeCalls.TryRemove(header.CallId, out _);
                CleanupCallEncryption(header.CallId);
                await CleanupCallStreamsAsync(header.CallId);
                if (OnCallEnded != null) await OnCallEnded(header.CallId);
                break;
            case SignalType.KeyExchange:
                await HandleKeyExchangeAsync(header.CallId, data, length, header);
                break;
        }
    }

    private async Task SendKeyExchangeAsync(Guid callId)
    {
        if (!_callEncryption.TryGetValue(callId, out var enc))
        {
            enc = CreateEncryption();
            _callEncryption[callId] = enc;
        }
        var writer = RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteCallSignal(writer, callId, SignalType.KeyExchange, enc.PublicKey);
        await _client.GetPrimaryConnection().SendAsync(writer.WrittenMemory, CancellationToken.None);
        writer.Reset();
    }

    private async Task HandleKeyExchangeAsync(Guid callId, byte[] data, int length, CallSignalHeader header)
    {
        var remotePublicKey = header.GetPayload(data.AsSpan(0, length)).ToArray();
        if (!_callEncryption.TryGetValue(callId, out var enc))
        {
            enc = CreateEncryption();
            _callEncryption[callId] = enc;
        }
        enc.DeriveKey(remotePublicKey, callId);

        if (_activeCalls.TryGetValue(callId, out var call) && !call.IsOutgoing && !call.KeySent)
        {
            call.KeySent = true;
            await SendKeyExchangeAsync(callId);
        }

        foreach (var (_, stream) in _mediaStreams)
        {
            if (stream.CallId == callId)
                stream.SetEncryption(enc);
        }
    }

    private void CleanupCallEncryption(Guid callId)
    {
        if (_callEncryption.TryRemove(callId, out var enc)) enc.Dispose();
    }

    private async Task CleanupCallStreamsAsync(Guid callId)
    {
        foreach (var (streamId, stream) in _mediaStreams)
        {
            if (stream.CallId == callId)
            {
                _mediaStreams.TryRemove(streamId, out _);
                await stream.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, stream) in _mediaStreams) await stream.DisposeAsync();
        _mediaStreams.Clear();
        foreach (var (_, controller) in _bitrateControllers) await controller.DisposeAsync();
        _bitrateControllers.Clear();
        foreach (var (_, enc) in _callEncryption) enc.Dispose();
        _callEncryption.Clear();
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
    public bool KeySent { get; set; }
}
