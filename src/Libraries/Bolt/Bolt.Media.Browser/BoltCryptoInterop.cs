namespace Bolt.Media.Browser;

/// <summary>
/// JS interop wrapper for Web Crypto encryption.
/// Creates <see cref="ExternalMediaEncryption"/> instances backed by the browser's Web Crypto API.
/// </summary>
public sealed class BoltCryptoInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private IJSObjectReference? _instance;
    private byte[]? _publicKey;

    public BoltCryptoInterop(IJSRuntime js) => _js = js;

    public bool IsInitialized => _publicKey != null;

    /// <summary>Load the JS module and generate an ECDH key pair.</summary>
    public async Task InitializeAsync()
    {
        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-crypto.js");
        _instance = await _module.InvokeAsync<IJSObjectReference>("create");
        await _instance.InvokeVoidAsync("init");
        _publicKey = await _instance.InvokeAsync<byte[]>("getPublicKey");
    }

    /// <summary>
    /// Create an <see cref="ExternalMediaEncryption"/> that delegates all crypto
    /// to the browser's Web Crypto API via this interop instance.
    /// Pass the returned value to <see cref="BoltMediaClient.SetEncryptionFactory"/>.
    /// </summary>
    public ExternalMediaEncryption CreateEncryption()
    {
        if (_instance is null || _publicKey is null)
            throw new InvalidOperationException("Call InitializeAsync first");

        var instance = _instance;

        return new ExternalMediaEncryption(
            publicKey: _publicKey,
            deriveKey: (remotePk, callId) =>
                instance.InvokeVoidAsync("deriveKey", remotePk, callId.ToByteArray()).AsTask(),
            encrypt: (data, seq, streamId) =>
                instance.InvokeAsync<byte[]>("encrypt", data, seq, streamId.ToByteArray()).AsTask(),
            decrypt: (data, seq, streamId) =>
                instance.InvokeAsync<byte[]>("decrypt", data, seq, streamId.ToByteArray()).AsTask()
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_instance is not null)
        {
            await _instance.InvokeVoidAsync("dispose");
            await _instance.DisposeAsync();
        }
        if (_module is not null) await _module.DisposeAsync();
    }
}
