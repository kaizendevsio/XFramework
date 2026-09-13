namespace Bolt.Media.Browser;

/// <summary>Only authenticated envelope plaintext belongs here. Never persist these sender keys.</summary>
public sealed record SFrameSenderKey(string SenderId, string Kid, byte[] Key);

/// <summary>Owns one bounded RFC9605 session. Does not authenticate directories or negotiate keys.</summary>
public sealed class BoltSFrameInterop(IJSRuntime js) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _pendingFrames = new(32, 32);
    private IJSObjectReference? _module;
    private IJSObjectReference? _session;
    private Guid _callId;
    private string? _localSenderId;
    private HashSet<string> _remoteSenders = new(StringComparer.Ordinal);
    private bool _ready;
    private long _generation;

    public bool IsReady => _ready;

    public async Task ConfigureAsync(Guid callId, string localSenderId)
    {
        if (callId == Guid.Empty || string.IsNullOrWhiteSpace(localSenderId)) throw new ArgumentException("Call identity is required.");
        await _gate.WaitAsync();
        try
        {
            if (_session is not null) throw new InvalidOperationException("Dispose the previous SFrame call before configuring another.");
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/Bolt.Media.Browser/bolt-sframe.mjs");
            _session = await _module.InvokeAsync<IJSObjectReference>("createSession", callId.ToString("D"), localSenderId);
            _callId = callId; _localSenderId = localSenderId;
        }
        finally { _gate.Release(); }
    }

    public async Task InstallEpochAsync(string epochId, string rosterBinding, SFrameSenderKey local, IReadOnlyList<SFrameSenderKey> remote)
    {
        _ready = false; Interlocked.Increment(ref _generation);
        await _gate.WaitAsync();
        try
        {
            if (_session is null || local.SenderId != _localSenderId) throw new InvalidOperationException("SFrame call not configured.");
            await _session.InvokeVoidAsync("installEpoch", new { epochId, rosterBinding, local, remote });
            _remoteSenders = remote.Select(x => x.SenderId).ToHashSet(StringComparer.Ordinal);
        }
        finally { _gate.Release(); }
    }

    public async Task ActivateEpochAsync(string epochId, string rosterBinding)
    {
        var generation = Interlocked.Read(ref _generation);
        await _gate.WaitAsync();
        try
        {
            if (_session is null) throw new InvalidOperationException("SFrame call not configured.");
            await _session.InvokeVoidAsync("activateEpoch", epochId, rosterBinding);
            _ready = generation == Interlocked.Read(ref _generation);
        }
        finally { _gate.Release(); }
    }

    public async Task PauseAsync()
    {
        _ready = false; Interlocked.Increment(ref _generation);
        await _gate.WaitAsync();
        try { if (_session is not null) await _session.InvokeVoidAsync("pause"); }
        finally { _gate.Release(); }
    }

    public IMediaEncryption ForStream(Guid callId, string senderId)
    {
        if (_session is null || _callId != callId || string.IsNullOrEmpty(senderId))
            throw new InvalidOperationException("Unconfigured SFrame call.");
        return new StreamEncryption(this, callId, senderId);
    }

    private async Task<byte[]> TransformAsync(bool sending, Guid callId, string senderId, byte[] data, uint sequence, uint timestamp, Guid streamId)
    {
        var generation = Interlocked.Read(ref _generation);
        if (!_ready || callId != _callId) throw new InvalidOperationException("SFrame epoch is paused.");
        if (data.Length is < 1 or > 5155 || !_pendingFrames.Wait(0))
            throw new InvalidOperationException("SFrame frame processing capacity exceeded.");
        try
        {
            await _gate.WaitAsync();
            try
            {
                if (!_ready || _session is null || callId != _callId || generation != Interlocked.Read(ref _generation))
                    throw new InvalidOperationException("SFrame epoch changed.");
                byte[] result;
                if (sending)
                {
                    if (senderId != _localSenderId) throw new InvalidOperationException("Cannot encrypt as another sender.");
                    result = await _session.InvokeAsync<byte[]>("encrypt", data, streamId.ToString("D"), sequence, timestamp);
                }
                else
                {
                    if (!_remoteSenders.Contains(senderId)) throw new InvalidOperationException("Sender is not in this epoch.");
                    result = await _session.InvokeAsync<byte[]>("decrypt", senderId, data, streamId.ToString("D"), sequence, timestamp);
                }
                if (!_ready || generation != Interlocked.Read(ref _generation))
                {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(result);
                    throw new InvalidOperationException("SFrame epoch changed during processing.");
                }
                return result;
            }
            finally { _gate.Release(); }
        }
        finally { _pendingFrames.Release(); }
    }

    public async Task EndCallAsync()
    {
        _ready = false; Interlocked.Increment(ref _generation);
        await _gate.WaitAsync();
        try
        {
            var session = _session;
            _session = null; _callId = Guid.Empty; _localSenderId = null; _remoteSenders.Clear();
            if (session is not null)
            {
                try { await session.InvokeVoidAsync("dispose"); }
                finally { await session.DisposeAsync(); }
            }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await EndCallAsync();
        if (_module is not null) { await _module.DisposeAsync(); _module = null; }
    }

    private sealed class StreamEncryption(BoltSFrameInterop owner, Guid callId, string senderId) : IMediaEncryption
    {
        public byte[] PublicKey => [];
        public bool IsReady => owner.IsReady && owner._callId == callId;
        public int AuthTagSize => 16;
        public void DeriveKey(ReadOnlySpan<byte> key, Guid callId) => throw new NotSupportedException("Use authenticated SFrame epoch envelopes.");
        public byte[] Encrypt(ReadOnlySpan<byte> data, uint sequence, Guid streamId) => throw new NotSupportedException("Use context-aware async encryption.");
        public byte[] Decrypt(ReadOnlySpan<byte> data, uint sequence, Guid streamId) => throw new NotSupportedException("Use context-aware async decryption.");
        public Task<byte[]> EncryptAsync(byte[] data, uint sequence, uint timestamp, Guid streamId)
            => owner.TransformAsync(true, callId, senderId, data, sequence, timestamp, streamId);
        public Task<byte[]> DecryptAsync(byte[] data, uint sequence, uint timestamp, Guid streamId)
            => owner.TransformAsync(false, callId, senderId, data, sequence, timestamp, streamId);
        public void Dispose() { /* The call owns its shared SFrame session. */ }
    }
}
