using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Bolt.Media;

/// <summary>
/// Abstraction for media frame encryption.
/// Two implementations:
/// - <see cref="MediaEncryption"/>: .NET-native (server, desktop, MAUI Hybrid)
/// - <see cref="ExternalMediaEncryption"/>: delegate-based for Blazor WASM (JS interop to Web Crypto)
/// </summary>
public interface IMediaEncryption : IDisposable
{
    /// <summary>ECDH public key in SubjectPublicKeyInfo DER format.</summary>
    byte[] PublicKey { get; }

    /// <summary>True once key exchange is complete and encrypt/decrypt are ready.</summary>
    bool IsReady { get; }

    /// <summary>Auth tag size appended to each encrypted frame.</summary>
    int AuthTagSize { get; }

    /// <summary>Derive the shared key from the remote peer's public key.</summary>
    void DeriveKey(ReadOnlySpan<byte> remotePublicKeyDer, Guid callId);

    /// <summary>Encrypt a media frame. Returns ciphertext + auth tag.</summary>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, uint sequenceNumber, Guid streamId);

    /// <summary>Decrypt a media frame (ciphertext + auth tag). Returns plaintext.</summary>
    byte[] Decrypt(ReadOnlySpan<byte> ciphertextWithTag, uint sequenceNumber, Guid streamId);

    /// <summary>Async variant of DeriveKey for environments where sync crypto is unsafe (e.g. Blazor WASM JS interop).</summary>
    Task DeriveKeyAsync(byte[] remotePublicKeyDer, Guid callId)
    {
        DeriveKey(remotePublicKeyDer, callId);
        return Task.CompletedTask;
    }

    /// <summary>Async variant of Encrypt for environments where sync crypto is unsafe (e.g. Blazor WASM JS interop).</summary>
    Task<byte[]> EncryptAsync(byte[] plaintext, uint sequenceNumber, Guid streamId)
        => Task.FromResult(Encrypt(plaintext, sequenceNumber, streamId));

    /// <summary>Async variant of Decrypt for environments where sync crypto is unsafe (e.g. Blazor WASM JS interop).</summary>
    Task<byte[]> DecryptAsync(byte[] ciphertextWithTag, uint sequenceNumber, Guid streamId)
        => Task.FromResult(Decrypt(ciphertextWithTag, sequenceNumber, streamId));
}

/// <summary>
/// .NET-native encryption using ECDH P-256 + AES-256-GCM.
/// Works on server, desktop, and MAUI Blazor Hybrid (NOT Blazor WASM).
///
/// Nonce: streamId[0..7] ++ sequenceNumber (4 bytes LE) = 12 bytes.
/// </summary>
public sealed class MediaEncryption : IMediaEncryption
{
    private readonly ECDiffieHellman _ecdh;
    private byte[]? _aesKey;
    private bool _disposed;

    public byte[] PublicKey { get; }
    public bool IsReady => _aesKey != null;
    public int AuthTagSize => 16;

    public MediaEncryption()
    {
        _ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        PublicKey = _ecdh.PublicKey.ExportSubjectPublicKeyInfo();
    }

    public void DeriveKey(ReadOnlySpan<byte> remotePublicKeyDer, Guid callId)
    {
        using var remoteEcdh = ECDiffieHellman.Create();
        remoteEcdh.ImportSubjectPublicKeyInfo(remotePublicKeyDer, out _);

        var sharedSecret = _ecdh.DeriveKeyMaterial(remoteEcdh.PublicKey);
        var salt = callId.ToByteArray();

        _aesKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            sharedSecret,
            outputLength: 32,
            salt: salt,
            info: "bolt-media-e2e"u8.ToArray());
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, uint sequenceNumber, Guid streamId)
    {
        if (_aesKey == null) throw new InvalidOperationException("Key not derived yet");

        Span<byte> nonce = stackalloc byte[12];
        BuildNonce(nonce, streamId, sequenceNumber);

        var output = new byte[plaintext.Length + AuthTagSize];

        using var aes = new AesGcm(_aesKey, AuthTagSize);
        aes.Encrypt(nonce, plaintext, output.AsSpan(0, plaintext.Length), output.AsSpan(plaintext.Length, AuthTagSize));

        return output;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertextWithTag, uint sequenceNumber, Guid streamId)
    {
        if (_aesKey == null) throw new InvalidOperationException("Key not derived yet");
        if (ciphertextWithTag.Length < AuthTagSize) throw new ArgumentException("Data too short for decryption");

        Span<byte> nonce = stackalloc byte[12];
        BuildNonce(nonce, streamId, sequenceNumber);

        var ciphertextLen = ciphertextWithTag.Length - AuthTagSize;
        var plaintext = new byte[ciphertextLen];

        using var aes = new AesGcm(_aesKey, AuthTagSize);
        aes.Decrypt(nonce, ciphertextWithTag[..ciphertextLen], ciphertextWithTag[ciphertextLen..], plaintext);

        return plaintext;
    }

    internal static void BuildNonce(Span<byte> nonce, Guid streamId, uint sequenceNumber)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        streamId.TryWriteBytes(guidBytes);
        guidBytes[..8].CopyTo(nonce);
        BinaryPrimitives.WriteUInt32LittleEndian(nonce[8..], sequenceNumber);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ecdh.Dispose();
        if (_aesKey != null)
            CryptographicOperations.ZeroMemory(_aesKey);
    }
}

/// <summary>
/// Delegate-based encryption for Blazor WASM.
/// Wraps external crypto (e.g., Web Crypto API via JS interop).
///
/// Usage in Blazor WASM:
/// <code>
/// var encryption = new ExternalMediaEncryption(
///     publicKey: await jsRuntime.InvokeAsync&lt;byte[]&gt;("boltCrypto.generateKeyPair"),
///     deriveKey: (remotePk, callId) => jsRuntime.InvokeVoidAsync("boltCrypto.deriveKey", remotePk, callId.ToString()),
///     encrypt: (data, seq, streamId) => jsRuntime.InvokeAsync&lt;byte[]&gt;("boltCrypto.encrypt", data, seq, streamId.ToString()),
///     decrypt: (data, seq, streamId) => jsRuntime.InvokeAsync&lt;byte[]&gt;("boltCrypto.decrypt", data, seq, streamId.ToString())
/// );
/// stream.SetEncryption(encryption);
/// </code>
/// </summary>
public sealed class ExternalMediaEncryption : IMediaEncryption
{
    private readonly Func<byte[], Guid, Task> _deriveKey;
    private readonly Func<byte[], uint, Guid, Task<byte[]>> _encrypt;
    private readonly Func<byte[], uint, Guid, Task<byte[]>> _decrypt;
    private bool _isReady;

    public byte[] PublicKey { get; }
    public bool IsReady => _isReady;
    public int AuthTagSize => 16;

    /// <summary>
    /// Create a delegate-based encryption provider.
    /// All crypto operations are delegated to the provided functions (typically JS interop).
    /// </summary>
    /// <param name="publicKey">Pre-generated ECDH public key (SPKI DER format).</param>
    /// <param name="deriveKey">Async function: (remotePublicKey, callId) → derives shared key.</param>
    /// <param name="encrypt">Async function: (plaintext, seqNumber, streamId) → ciphertext+tag.</param>
    /// <param name="decrypt">Async function: (ciphertext+tag, seqNumber, streamId) → plaintext.</param>
    public ExternalMediaEncryption(
        byte[] publicKey,
        Func<byte[], Guid, Task> deriveKey,
        Func<byte[], uint, Guid, Task<byte[]>> encrypt,
        Func<byte[], uint, Guid, Task<byte[]>> decrypt)
    {
        PublicKey = publicKey;
        _deriveKey = deriveKey;
        _encrypt = encrypt;
        _decrypt = decrypt;
    }

    public void DeriveKey(ReadOnlySpan<byte> remotePublicKeyDer, Guid callId)
    {
        // Synchronous wrapper — safe only when the delegate returns an already-completed Task
        var task = _deriveKey(remotePublicKeyDer.ToArray(), callId);
        task.GetAwaiter().GetResult();
        _isReady = true;
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, uint sequenceNumber, Guid streamId)
    {
        if (!_isReady) throw new InvalidOperationException("Key not derived yet");
        return _encrypt(plaintext.ToArray(), sequenceNumber, streamId).GetAwaiter().GetResult();
    }

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertextWithTag, uint sequenceNumber, Guid streamId)
    {
        if (!_isReady) throw new InvalidOperationException("Key not derived yet");
        return _decrypt(ciphertextWithTag.ToArray(), sequenceNumber, streamId).GetAwaiter().GetResult();
    }

    /// <summary>Truly async — preferred over DeriveKey in Blazor WASM to avoid deadlocks.</summary>
    public async Task DeriveKeyAsync(byte[] remotePublicKeyDer, Guid callId)
    {
        await _deriveKey(remotePublicKeyDer, callId);
        _isReady = true;
    }

    /// <summary>Truly async — preferred over Encrypt in Blazor WASM to avoid deadlocks.</summary>
    public async Task<byte[]> EncryptAsync(byte[] plaintext, uint sequenceNumber, Guid streamId)
    {
        if (!_isReady) throw new InvalidOperationException("Key not derived yet");
        return await _encrypt(plaintext, sequenceNumber, streamId);
    }

    /// <summary>Truly async — preferred over Decrypt in Blazor WASM to avoid deadlocks.</summary>
    public async Task<byte[]> DecryptAsync(byte[] ciphertextWithTag, uint sequenceNumber, Guid streamId)
    {
        if (!_isReady) throw new InvalidOperationException("Key not derived yet");
        return await _decrypt(ciphertextWithTag, sequenceNumber, streamId);
    }

    public void Dispose() { }
}
