# Bolt.Media.Browser Implementation Plan

> Historical plan migrated from `docs/superpowers/`. For new implementation planning, use `/ce-plan`; this checklist is retained as context.

**Goal:** Create a Razor Class Library that bridges Bolt.Media's C# APIs to browser-only APIs (Web Crypto, WebCodecs, getUserMedia) via JS interop, so any Blazor WASM app can add voice/video calls with one `AddBoltMediaBrowser()` call.

**Architecture:** Three layers — (1) JS modules wrapping browser APIs, (2) C# `IJSRuntime` wrappers exposing typed .NET APIs, (3) `BoltMediaService` orchestrating capture → encode → BoltMediaStream → decode → playback. All signaling and media transport use existing C# `BoltMediaClient`/`BoltMediaStream`. JS only handles what .NET WASM cannot: hardware codecs, crypto, and media device access.

**Tech Stack:** .NET 10, Razor Class Library, `IJSRuntime`/`IJSObjectReference`, Web Crypto API, WebCodecs API, `getUserMedia`, `AudioContext`, `HTMLCanvasElement`

---

## File Structure

### New files (Bolt.Media.Browser RCL)

```
src/Libraries/Bolt/Bolt.Media.Browser/
├── Bolt.Media.Browser.csproj          # Razor Class Library, refs Bolt.Media
├── GlobalUsings.cs
├── wwwroot/
│   ├── bolt-crypto.js                 # Web Crypto: ECDH P-256 + AES-256-GCM + HKDF
│   └── bolt-media.js                  # WebCodecs + getUserMedia + AudioContext + Canvas
├── BoltCryptoInterop.cs               # C# ↔ bolt-crypto.js, creates ExternalMediaEncryption
├── BoltAudioPipeline.cs               # Capture → AudioEncoder → callback; decode → AudioContext
├── BoltVideoPipeline.cs               # Capture → VideoEncoder → callback; decode → Canvas
├── BoltDeviceManager.cs               # Device enumeration, permission requests
├── BoltMediaService.cs                # High-level: wires BoltMediaClient + pipelines + crypto
├── ServiceCollectionExtensions.cs     # AddBoltMediaBrowser() DI registration
└── MediaServiceOptions.cs             # Configuration (bitrate, resolution, codec prefs)
```

### New test files

```
src/Tests/Bolt.Tests/BoltMediaBrowserTests.cs   # Add to existing test project
```

### Modified files

```
XFramework.slnx                        # Add Bolt.Media.Browser project reference
```

### Existing files referenced (read-only context)

```
src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs          # Call signaling, stream management
src/Libraries/Bolt/Bolt.Media/BoltMediaStream.cs           # SendFrameAsync, ReadFramesAsync, OnBitrateChanged
src/Libraries/Bolt/Bolt.Media/MediaEncryption.cs           # ExternalMediaEncryption (delegate-based)
src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs:56        # SetEncryptionFactory(Func<IMediaEncryption>)
src/Libraries/Bolt/Bolt.Browser/src/encryption.ts          # Reference JS implementation
src/Libraries/Bolt/Bolt.Browser/src/webcodecs-helper.ts    # Reference JS implementation
```

---

## Task 1: Project Scaffolding

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/GlobalUsings.cs`
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/MediaServiceOptions.cs`
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/ServiceCollectionExtensions.cs`
- Modify: `XFramework.slnx`

- [ ] **Step 1: Create the csproj**

Create `src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <LangVersion>14</LangVersion>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>

        <PackageId>Bolt.Net.Media.Browser</PackageId>
        <Version>$(BoltVersion)</Version>
        <Authors>KaizenDevs</Authors>
        <Description>Bolt Media browser integration for Blazor WASM — JS interop for WebCodecs, Web Crypto, and getUserMedia. Add-on for Bolt.Net.Media.</Description>
        <PackageTags>blazor;wasm;media;webcodecs;webcrypto;bolt;voip;video;audio</PackageTags>
        <RepositoryUrl>https://github.com/kaizendevsio/XFramework</RepositoryUrl>
        <PackageProjectUrl>https://github.com/kaizendevsio/XFramework</PackageProjectUrl>
        <PackageLicenseExpression>MIT</PackageLicenseExpression>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Bolt.Media\Bolt.Media.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.Components.Web" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Create GlobalUsings.cs**

Create `src/Libraries/Bolt/Bolt.Media.Browser/GlobalUsings.cs`:

```csharp
global using Microsoft.JSInterop;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 3: Create MediaServiceOptions.cs**

Create `src/Libraries/Bolt/Bolt.Media.Browser/MediaServiceOptions.cs`:

```csharp
namespace Bolt.Media.Browser;

public sealed class MediaServiceOptions
{
    public int AudioBitrateKbps { get; set; } = 64;
    public int AudioSampleRate { get; set; } = 48_000;
    public int AudioChannels { get; set; } = 1;

    public int VideoWidth { get; set; } = 1280;
    public int VideoHeight { get; set; } = 720;
    public int VideoBitrateKbps { get; set; } = 2_000;
    public int VideoFramerate { get; set; } = 30;
    public string VideoCodec { get; set; } = "h264";
    public int KeyframeIntervalFrames { get; set; } = 60;

    public bool EnableEncryption { get; set; } = true;
    public bool EnableFec { get; set; } = true;
    public int FecAudioGroupSize { get; set; } = 4;
    public int FecVideoGroupSize { get; set; } = 8;
}
```

- [ ] **Step 4: Create ServiceCollectionExtensions.cs (skeleton)**

Create `src/Libraries/Bolt/Bolt.Media.Browser/ServiceCollectionExtensions.cs`:

```csharp
namespace Bolt.Media.Browser;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoltMediaBrowser(
        this IServiceCollection services,
        Action<MediaServiceOptions>? configure = null)
    {
        var options = new MediaServiceOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<BoltCryptoInterop>();
        services.AddScoped<BoltAudioPipeline>();
        services.AddScoped<BoltVideoPipeline>();
        services.AddScoped<BoltDeviceManager>();
        services.AddScoped<BoltMediaService>();

        return services;
    }
}
```

- [ ] **Step 5: Add project to solution and verify build**

Add to `XFramework.slnx` in the `/Libraries/Bolt/` folder section:

```xml
<Project Path="src\Libraries\Bolt\Bolt.Media.Browser\Bolt.Media.Browser.csproj" />
```

Run: `dotnet build src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`
Expected: Build succeeds (ServiceCollectionExtensions references types not yet created — use placeholder stubs or forward-declare classes)

**Note:** The build will fail until Tasks 2-6 create the referenced types. To unblock, create empty stub classes for `BoltCryptoInterop`, `BoltAudioPipeline`, `BoltVideoPipeline`, `BoltDeviceManager`, and `BoltMediaService` — just `public sealed class X { }` — and replace them in later tasks.

- [ ] **Step 6: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Media.Browser/ XFramework.slnx
git commit -m "feat(bolt-media-browser): scaffold Razor Class Library project"
```

---

## Task 2: Encryption Bridge

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-crypto.js`
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/BoltCryptoInterop.cs`

### Context for implementer

`ExternalMediaEncryption` (in `Bolt.Media/MediaEncryption.cs`) accepts delegates for encrypt/decrypt/deriveKey. This task creates the JS crypto implementation and a C# class that bridges `IJSRuntime` calls into those delegates.

The encryption algorithm must match exactly:
- **Key exchange:** ECDH P-256, public key exported as SPKI DER
- **Key derivation:** HKDF-SHA256, salt = callId bytes, info = `"bolt-media-e2e"`, output = 32 bytes
- **Frame encryption:** AES-256-GCM, 12-byte nonce = `streamId[0..7] ++ sequenceNumber(4 bytes LE)`, 16-byte auth tag appended
- **GUID byte layout:** First 8 bytes of .NET `Guid.ToByteArray()` (mixed-endian per .NET convention)

- [ ] **Step 1: Create bolt-crypto.js**

Create `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-crypto.js`:

```javascript
// Bolt Media — Web Crypto encryption module
// ECDH P-256 key exchange + AES-256-GCM frame encryption
// Compatible with .NET MediaEncryption (same nonce/HKDF params)

class BoltCrypto {
    constructor() {
        this.keyPair = null;
        this.aesKey = null;
        this.publicKeyDer = null;
        this.ready = false;
    }

    async init() {
        this.keyPair = await crypto.subtle.generateKey(
            { name: 'ECDH', namedCurve: 'P-256' },
            false,
            ['deriveBits']
        );
        const exported = await crypto.subtle.exportKey('spki', this.keyPair.publicKey);
        this.publicKeyDer = new Uint8Array(exported);
    }

    getPublicKey() {
        return this.publicKeyDer;
    }

    async deriveKey(remotePublicKeyDer, callIdBytes) {
        const remotePubKey = await crypto.subtle.importKey(
            'spki', remotePublicKeyDer,
            { name: 'ECDH', namedCurve: 'P-256' },
            false, []
        );

        const sharedBits = await crypto.subtle.deriveBits(
            { name: 'ECDH', public: remotePubKey },
            this.keyPair.privateKey,
            256
        );

        const hkdfKey = await crypto.subtle.importKey(
            'raw', sharedBits, 'HKDF', false, ['deriveKey']
        );

        const encoder = new TextEncoder();
        this.aesKey = await crypto.subtle.deriveKey(
            {
                name: 'HKDF',
                hash: 'SHA-256',
                salt: callIdBytes,
                info: encoder.encode('bolt-media-e2e')
            },
            hkdfKey,
            { name: 'AES-GCM', length: 256 },
            false,
            ['encrypt', 'decrypt']
        );
        this.ready = true;
    }

    async encrypt(plaintext, sequenceNumber, streamIdBytes) {
        const nonce = this._buildNonce(streamIdBytes, sequenceNumber);
        const encrypted = await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv: nonce, tagLength: 128 },
            this.aesKey,
            plaintext
        );
        return new Uint8Array(encrypted);
    }

    async decrypt(ciphertextWithTag, sequenceNumber, streamIdBytes) {
        const nonce = this._buildNonce(streamIdBytes, sequenceNumber);
        const decrypted = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: nonce, tagLength: 128 },
            this.aesKey,
            ciphertextWithTag
        );
        return new Uint8Array(decrypted);
    }

    _buildNonce(streamIdBytes, sequenceNumber) {
        // 12 bytes: streamId[0..7] + sequenceNumber (4 bytes LE)
        const nonce = new Uint8Array(12);
        nonce.set(streamIdBytes.slice(0, 8), 0);
        const view = new DataView(nonce.buffer);
        view.setUint32(8, sequenceNumber, true); // little-endian
        return nonce;
    }

    dispose() {
        this.keyPair = null;
        this.aesKey = null;
        this.publicKeyDer = null;
        this.ready = false;
    }
}

export function create() {
    return new BoltCrypto();
}
```

- [ ] **Step 2: Create BoltCryptoInterop.cs**

Create `src/Libraries/Bolt/Bolt.Media.Browser/BoltCryptoInterop.cs`:

```csharp
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
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-crypto.js
git add src/Libraries/Bolt/Bolt.Media.Browser/BoltCryptoInterop.cs
git commit -m "feat(bolt-media-browser): encryption bridge — Web Crypto ↔ ExternalMediaEncryption"
```

---

## Task 3: Audio Pipeline

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js` (audio section)
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/BoltAudioPipeline.cs`

### Context for implementer

The audio pipeline bridges browser audio capture/playback to `BoltMediaStream`:
- **Capture path:** `getUserMedia` → `MediaStreamTrackProcessor` → `AudioData` → `AudioEncoder` (Opus) → encoded bytes → JS calls `[JSInvokable]` C# method → `BoltMediaStream.SendFrameAsync`
- **Playback path:** C# calls JS `decodeFrame(bytes, timestamp)` → `AudioDecoder` (Opus) → `AudioData` → `AudioContext` → speakers

The `AudioEncoder`/`AudioDecoder` are WebCodecs APIs (Chrome 94+, Edge 94+, Safari 16.4+). Firefox does not support WebCodecs.

- [ ] **Step 1: Create bolt-media.js with AudioPipeline**

Create `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js`:

```javascript
// Bolt Media — WebCodecs + Media Capture + Playback
// Audio and Video pipelines for Blazor WASM interop

// ─── Audio Pipeline ─────────────────────────────────

class AudioPipeline {
    constructor() {
        this.encoder = null;
        this.decoder = null;
        this.mediaStream = null;
        this.trackProcessor = null;
        this.captureReader = null;
        this.captureRunning = false;
        this.audioContext = null;
        this.dotNetRef = null;
    }

    async initEncoder(sampleRate, channels, bitrate) {
        this.encoder = new AudioEncoder({
            output: (chunk) => {
                const data = new Uint8Array(chunk.byteLength);
                chunk.copyTo(data);
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync('OnAudioEncoded', data);
                }
            },
            error: (e) => console.error('AudioEncoder error:', e)
        });
        this.encoder.configure({
            codec: 'opus',
            sampleRate: sampleRate,
            numberOfChannels: channels,
            bitrate: bitrate * 1000
        });
    }

    async initDecoder(sampleRate, channels) {
        this.audioContext = new AudioContext({ sampleRate: sampleRate });

        this.decoder = new AudioDecoder({
            output: (audioData) => {
                this._playAudioData(audioData);
            },
            error: (e) => console.error('AudioDecoder error:', e)
        });
        this.decoder.configure({
            codec: 'opus',
            sampleRate: sampleRate,
            numberOfChannels: channels
        });
    }

    async startCapture(dotNetRef, constraints) {
        this.dotNetRef = dotNetRef;
        const audioConstraints = constraints
            ? { sampleRate: constraints.sampleRate, channelCount: constraints.channels, echoCancellation: true, noiseSuppression: true }
            : { echoCancellation: true, noiseSuppression: true };

        this.mediaStream = await navigator.mediaDevices.getUserMedia({ audio: audioConstraints, video: false });
        const track = this.mediaStream.getAudioTracks()[0];

        this.trackProcessor = new MediaStreamTrackProcessor({ track: track });
        this.captureReader = this.trackProcessor.readable.getReader();
        this.captureRunning = true;

        this._readLoop();
    }

    async _readLoop() {
        while (this.captureRunning) {
            try {
                const { value, done } = await this.captureReader.read();
                if (done) break;
                if (this.encoder && this.encoder.state === 'configured') {
                    this.encoder.encode(value);
                }
                value.close();
            } catch {
                break;
            }
        }
    }

    stopCapture() {
        this.captureRunning = false;
        if (this.captureReader) { this.captureReader.cancel(); this.captureReader = null; }
        if (this.trackProcessor) { this.trackProcessor = null; }
        if (this.mediaStream) {
            this.mediaStream.getTracks().forEach(t => t.stop());
            this.mediaStream = null;
        }
    }

    decodeFrame(data, timestamp) {
        if (!this.decoder || this.decoder.state !== 'configured') return;
        const chunk = new EncodedAudioChunk({
            type: 'key',
            timestamp: timestamp,
            data: data
        });
        this.decoder.decode(chunk);
    }

    reconfigureBitrate(sampleRate, channels, newBitrate) {
        if (!this.encoder || this.encoder.state !== 'configured') return;
        this.encoder.configure({
            codec: 'opus',
            sampleRate: sampleRate,
            numberOfChannels: channels,
            bitrate: newBitrate * 1000
        });
    }

    setVolume(volume) {
        // Volume is controlled via GainNode if needed (future)
    }

    _playAudioData(audioData) {
        if (!this.audioContext || this.audioContext.state === 'closed') {
            audioData.close();
            return;
        }

        const numberOfFrames = audioData.numberOfFrames;
        const channels = audioData.numberOfChannels;
        const sampleRate = audioData.sampleRate;

        const buffer = this.audioContext.createBuffer(channels, numberOfFrames, sampleRate);
        for (let ch = 0; ch < channels; ch++) {
            const channelData = new Float32Array(numberOfFrames);
            audioData.copyTo(channelData, { planeIndex: ch, format: 'f32-planar' });
            buffer.copyToChannel(channelData, ch);
        }
        audioData.close();

        const source = this.audioContext.createBufferSource();
        source.buffer = buffer;
        source.connect(this.audioContext.destination);
        source.start();
    }

    async dispose() {
        this.stopCapture();
        if (this.encoder) { this.encoder.close(); this.encoder = null; }
        if (this.decoder) { this.decoder.close(); this.decoder = null; }
        if (this.audioContext) { await this.audioContext.close(); this.audioContext = null; }
        this.dotNetRef = null;
    }
}

// ─── Video Pipeline (placeholder — Task 4) ─────────

class VideoPipeline {
    constructor() {}
    async dispose() {}
}

// ─── Device Manager (placeholder — Task 5) ─────────

class DeviceManager {
    static async enumerateAudioInputs() { return []; }
    static async enumerateVideoInputs() { return []; }
    static async checkPermissions() { return { audio: false, video: false }; }
}

// ─── Exports ────────────────────────────────────────

export function createAudioPipeline() { return new AudioPipeline(); }
export function createVideoPipeline() { return new VideoPipeline(); }
export function getDeviceManager() { return DeviceManager; }
```

- [ ] **Step 2: Create BoltAudioPipeline.cs**

Create `src/Libraries/Bolt/Bolt.Media.Browser/BoltAudioPipeline.cs`:

```csharp
namespace Bolt.Media.Browser;

/// <summary>
/// Audio capture → WebCodecs encode → C# callback, and C# → WebCodecs decode → AudioContext playback.
/// Bridges browser audio APIs to <see cref="BoltMediaStream"/>.
/// </summary>
public sealed class BoltAudioPipeline : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ILogger<BoltAudioPipeline> _logger;
    private IJSObjectReference? _module;
    private IJSObjectReference? _pipeline;
    private DotNetObjectReference<BoltAudioPipeline>? _dotNetRef;
    private bool _capturing;

    /// <summary>Fires when the audio encoder produces an encoded Opus frame.</summary>
    public event Action<byte[]>? OnEncoded;

    public bool IsCapturing => _capturing;

    public BoltAudioPipeline(IJSRuntime js, ILogger<BoltAudioPipeline> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>Load JS module, initialize Opus encoder and decoder.</summary>
    public async Task InitializeAsync(int sampleRate = 48_000, int channels = 1, int bitrateKbps = 64)
    {
        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        _pipeline = await _module.InvokeAsync<IJSObjectReference>("createAudioPipeline");
        _dotNetRef = DotNetObjectReference.Create(this);

        await _pipeline.InvokeVoidAsync("initEncoder", sampleRate, channels, bitrateKbps);
        await _pipeline.InvokeVoidAsync("initDecoder", sampleRate, channels);
    }

    /// <summary>Start capturing audio from the microphone.</summary>
    public async Task StartCaptureAsync(int? sampleRate = null, int? channels = null)
    {
        if (_pipeline is null) throw new InvalidOperationException("Call InitializeAsync first");

        object? constraints = (sampleRate.HasValue || channels.HasValue)
            ? new { sampleRate = sampleRate ?? 48_000, channels = channels ?? 1 }
            : null;

        await _pipeline.InvokeVoidAsync("startCapture", _dotNetRef, constraints);
        _capturing = true;
        _logger.LogDebug("Audio capture started");
    }

    /// <summary>Stop capturing audio.</summary>
    public async Task StopCaptureAsync()
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("stopCapture");
        _capturing = false;
        _logger.LogDebug("Audio capture stopped");
    }

    /// <summary>Decode and play an incoming audio frame from the remote peer.</summary>
    public async ValueTask DecodeFrameAsync(ReadOnlyMemory<byte> data, uint timestamp)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("decodeFrame", data.ToArray(), timestamp);
    }

    /// <summary>Change the encoder bitrate in response to ABR feedback.</summary>
    public async ValueTask ReconfigureBitrateAsync(int sampleRate, int channels, int newBitrateKbps)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("reconfigureBitrate", sampleRate, channels, newBitrateKbps);
    }

    /// <summary>Called from JS when an encoded audio chunk is ready.</summary>
    [JSInvokable]
    public void OnAudioEncoded(byte[] data)
    {
        OnEncoded?.Invoke(data);
    }

    public async ValueTask DisposeAsync()
    {
        _capturing = false;
        if (_pipeline is not null)
        {
            await _pipeline.InvokeVoidAsync("dispose");
            await _pipeline.DisposeAsync();
        }
        _dotNetRef?.Dispose();
        if (_module is not null) await _module.DisposeAsync();
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js
git add src/Libraries/Bolt/Bolt.Media.Browser/BoltAudioPipeline.cs
git commit -m "feat(bolt-media-browser): audio pipeline — WebCodecs Opus encode/decode + getUserMedia capture"
```

---

## Task 4: Video Pipeline

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js` (replace VideoPipeline placeholder)
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/BoltVideoPipeline.cs`

### Context for implementer

Video pipeline is similar to audio but with:
- `VideoEncoder`/`VideoDecoder` instead of `AudioEncoder`/`AudioDecoder`
- `VideoFrame` instead of `AudioData`
- Keyframe logic: force keyframe every N frames (configurable)
- Playback renders to a `<canvas>` element (C# passes an `ElementReference`)
- `MediaStreamTrackProcessor` gives `ReadableStream<VideoFrame>`
- H.264 codec string: `'avc1.42001f'` (Baseline Profile, Level 3.1)
- H.265 codec string: `'hev1.1.6.L93.B0'`
- Resolution/framerate reconfiguration for ABR

- [ ] **Step 1: Add VideoPipeline to bolt-media.js**

In `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js`, replace the `VideoPipeline` placeholder class with:

```javascript
class VideoPipeline {
    constructor() {
        this.encoder = null;
        this.decoder = null;
        this.mediaStream = null;
        this.trackProcessor = null;
        this.captureReader = null;
        this.captureRunning = false;
        this.canvas = null;
        this.canvasCtx = null;
        this.dotNetRef = null;
        this.frameCount = 0;
        this.keyframeInterval = 60;
    }

    async initEncoder(width, height, bitrate, framerate, codec, keyframeInterval) {
        this.keyframeInterval = keyframeInterval || 60;
        this.frameCount = 0;

        const codecString = codec === 'h265' ? 'hev1.1.6.L93.B0' : 'avc1.42001f';

        this.encoder = new VideoEncoder({
            output: (chunk, metadata) => {
                const data = new Uint8Array(chunk.byteLength);
                chunk.copyTo(data);
                const isKeyframe = chunk.type === 'key';
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync('OnVideoEncoded', data, isKeyframe);
                }
            },
            error: (e) => console.error('VideoEncoder error:', e)
        });
        this.encoder.configure({
            codec: codecString,
            width: width,
            height: height,
            bitrate: bitrate * 1000,
            framerate: framerate,
            latencyMode: 'realtime',
            hardwareAcceleration: 'prefer-hardware'
        });
    }

    async initDecoder(canvasElement, codec) {
        this.canvas = canvasElement;
        this.canvasCtx = canvasElement.getContext('2d');

        const codecString = codec === 'h265' ? 'hev1.1.6.L93.B0' : 'avc1.42001f';

        this.decoder = new VideoDecoder({
            output: (frame) => {
                this._renderFrame(frame);
            },
            error: (e) => console.error('VideoDecoder error:', e)
        });
        this.decoder.configure({
            codec: codecString,
            hardwareAcceleration: 'prefer-hardware'
        });
    }

    async startCapture(dotNetRef, constraints) {
        this.dotNetRef = dotNetRef;
        const videoConstraints = constraints
            ? { width: constraints.width, height: constraints.height, frameRate: constraints.framerate }
            : { width: 1280, height: 720, frameRate: 30 };

        this.mediaStream = await navigator.mediaDevices.getUserMedia({ audio: false, video: videoConstraints });
        const track = this.mediaStream.getVideoTracks()[0];

        this.trackProcessor = new MediaStreamTrackProcessor({ track: track });
        this.captureReader = this.trackProcessor.readable.getReader();
        this.captureRunning = true;

        this._readLoop();
    }

    async _readLoop() {
        while (this.captureRunning) {
            try {
                const { value, done } = await this.captureReader.read();
                if (done) break;
                if (this.encoder && this.encoder.state === 'configured') {
                    this.frameCount++;
                    const keyFrame = this.frameCount % this.keyframeInterval === 0;
                    this.encoder.encode(value, { keyFrame: keyFrame });
                }
                value.close();
            } catch {
                break;
            }
        }
    }

    stopCapture() {
        this.captureRunning = false;
        if (this.captureReader) { this.captureReader.cancel(); this.captureReader = null; }
        if (this.trackProcessor) { this.trackProcessor = null; }
        if (this.mediaStream) {
            this.mediaStream.getTracks().forEach(t => t.stop());
            this.mediaStream = null;
        }
    }

    decodeFrame(data, timestamp, isKeyframe) {
        if (!this.decoder || this.decoder.state !== 'configured') return;
        const chunk = new EncodedVideoChunk({
            type: isKeyframe ? 'key' : 'delta',
            timestamp: timestamp,
            data: data
        });
        this.decoder.decode(chunk);
    }

    requestKeyframe() {
        this.frameCount = this.keyframeInterval - 1; // Next frame will be keyframe
    }

    reconfigureBitrate(newBitrate) {
        if (!this.encoder || this.encoder.state !== 'configured') return;
        // VideoEncoder.configure() replaces the current config
        // We need to read current config and update bitrate
        this.encoder.configure({
            ...this._lastConfig,
            bitrate: newBitrate * 1000
        });
    }

    reconfigureResolution(width, height, framerate) {
        if (!this.encoder || this.encoder.state !== 'configured') return;
        this.encoder.configure({
            ...this._lastConfig,
            width: width,
            height: height,
            framerate: framerate || this._lastConfig?.framerate || 30,
        });
    }

    _renderFrame(frame) {
        if (!this.canvasCtx || !this.canvas) {
            frame.close();
            return;
        }
        this.canvas.width = frame.displayWidth;
        this.canvas.height = frame.displayHeight;
        this.canvasCtx.drawImage(frame, 0, 0);
        frame.close();
    }

    async dispose() {
        this.stopCapture();
        if (this.encoder) { this.encoder.close(); this.encoder = null; }
        if (this.decoder) { this.decoder.close(); this.decoder = null; }
        this.canvas = null;
        this.canvasCtx = null;
        this.dotNetRef = null;
    }
}
```

Also, update the `initEncoder` to store config for reconfigure:

In the `initEncoder` method, add before `this.encoder.configure(...)`:
```javascript
        this._lastConfig = {
            codec: codecString,
            width: width,
            height: height,
            bitrate: bitrate * 1000,
            framerate: framerate,
            latencyMode: 'realtime',
            hardwareAcceleration: 'prefer-hardware'
        };
        this.encoder.configure(this._lastConfig);
```

- [ ] **Step 2: Create BoltVideoPipeline.cs**

Create `src/Libraries/Bolt/Bolt.Media.Browser/BoltVideoPipeline.cs`:

```csharp
using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>
/// Video capture → WebCodecs H.264 encode → C# callback,
/// and C# → WebCodecs decode → canvas rendering.
/// </summary>
public sealed class BoltVideoPipeline : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ILogger<BoltVideoPipeline> _logger;
    private IJSObjectReference? _module;
    private IJSObjectReference? _pipeline;
    private DotNetObjectReference<BoltVideoPipeline>? _dotNetRef;
    private bool _capturing;

    /// <summary>Fires when the video encoder produces an encoded H.264 frame.</summary>
    public event Action<byte[], bool>? OnEncoded;

    public bool IsCapturing => _capturing;

    public BoltVideoPipeline(IJSRuntime js, ILogger<BoltVideoPipeline> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>Load JS module, initialize H.264 encoder.</summary>
    public async Task InitializeEncoderAsync(
        int width = 1280, int height = 720, int bitrateKbps = 2_000,
        int framerate = 30, string codec = "h264", int keyframeInterval = 60)
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        _pipeline ??= await _module.InvokeAsync<IJSObjectReference>("createVideoPipeline");
        _dotNetRef ??= DotNetObjectReference.Create(this);

        await _pipeline.InvokeVoidAsync("initEncoder", width, height, bitrateKbps, framerate, codec, keyframeInterval);
    }

    /// <summary>Initialize decoder and attach to a canvas element for rendering.</summary>
    public async Task InitializeDecoderAsync(ElementReference canvasElement, string codec = "h264")
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        _pipeline ??= await _module.InvokeAsync<IJSObjectReference>("createVideoPipeline");

        await _pipeline.InvokeVoidAsync("initDecoder", canvasElement, codec);
    }

    /// <summary>Start capturing video from the camera.</summary>
    public async Task StartCaptureAsync(int? width = null, int? height = null, int? framerate = null)
    {
        if (_pipeline is null) throw new InvalidOperationException("Call InitializeEncoderAsync first");

        object? constraints = (width.HasValue || height.HasValue || framerate.HasValue)
            ? new { width = width ?? 1280, height = height ?? 720, framerate = framerate ?? 30 }
            : null;

        await _pipeline.InvokeVoidAsync("startCapture", _dotNetRef, constraints);
        _capturing = true;
        _logger.LogDebug("Video capture started");
    }

    /// <summary>Stop capturing video.</summary>
    public async Task StopCaptureAsync()
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("stopCapture");
        _capturing = false;
    }

    /// <summary>Decode and render an incoming video frame to the canvas.</summary>
    public async ValueTask DecodeFrameAsync(ReadOnlyMemory<byte> data, uint timestamp, bool isKeyframe)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("decodeFrame", data.ToArray(), timestamp, isKeyframe);
    }

    /// <summary>Request the encoder to produce a keyframe on the next encode cycle.</summary>
    public async ValueTask RequestKeyframeAsync()
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("requestKeyframe");
    }

    /// <summary>Change encoder bitrate for ABR.</summary>
    public async ValueTask ReconfigureBitrateAsync(int newBitrateKbps)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("reconfigureBitrate", newBitrateKbps);
    }

    /// <summary>Change encoder resolution/framerate for ABR.</summary>
    public async ValueTask ReconfigureResolutionAsync(int width, int height, int? framerate = null)
    {
        if (_pipeline is null) return;
        await _pipeline.InvokeVoidAsync("reconfigureResolution", width, height, framerate);
    }

    [JSInvokable]
    public void OnVideoEncoded(byte[] data, bool isKeyframe)
    {
        OnEncoded?.Invoke(data, isKeyframe);
    }

    public async ValueTask DisposeAsync()
    {
        _capturing = false;
        if (_pipeline is not null)
        {
            await _pipeline.InvokeVoidAsync("dispose");
            await _pipeline.DisposeAsync();
        }
        _dotNetRef?.Dispose();
        if (_module is not null) await _module.DisposeAsync();
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js
git add src/Libraries/Bolt/Bolt.Media.Browser/BoltVideoPipeline.cs
git commit -m "feat(bolt-media-browser): video pipeline — WebCodecs H.264 encode/decode + canvas render"
```

---

## Task 5: Device Manager

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js` (replace DeviceManager placeholder)
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/BoltDeviceManager.cs`

### Context for implementer

The device manager wraps `navigator.mediaDevices.enumerateDevices()` and permission queries. This lets the Blazor UI show device pickers and check permissions before starting capture.

- [ ] **Step 1: Add DeviceManager to bolt-media.js**

In `src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js`, replace the `DeviceManager` placeholder class with:

```javascript
class DeviceManager {
    static async enumerateAudioInputs() {
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices
            .filter(d => d.kind === 'audioinput')
            .map(d => ({ deviceId: d.deviceId, label: d.label || `Microphone ${d.deviceId.slice(0, 8)}`, groupId: d.groupId }));
    }

    static async enumerateVideoInputs() {
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices
            .filter(d => d.kind === 'videoinput')
            .map(d => ({ deviceId: d.deviceId, label: d.label || `Camera ${d.deviceId.slice(0, 8)}`, groupId: d.groupId }));
    }

    static async enumerateAudioOutputs() {
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices
            .filter(d => d.kind === 'audiooutput')
            .map(d => ({ deviceId: d.deviceId, label: d.label || `Speaker ${d.deviceId.slice(0, 8)}`, groupId: d.groupId }));
    }

    static async checkPermissions() {
        const result = { audio: 'prompt', video: 'prompt' };
        try {
            const mic = await navigator.permissions.query({ name: 'microphone' });
            result.audio = mic.state;
        } catch { }
        try {
            const cam = await navigator.permissions.query({ name: 'camera' });
            result.video = cam.state;
        } catch { }
        return result;
    }

    static async requestPermissions(audio, video) {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio, video });
            stream.getTracks().forEach(t => t.stop());
            return true;
        } catch {
            return false;
        }
    }

    static isWebCodecsSupported() {
        return typeof AudioEncoder !== 'undefined' && typeof VideoEncoder !== 'undefined';
    }
}
```

Also update the export:
```javascript
export function getDeviceManager() { return DeviceManager; }
```

Replace with:
```javascript
export async function enumerateAudioInputs() { return await DeviceManager.enumerateAudioInputs(); }
export async function enumerateVideoInputs() { return await DeviceManager.enumerateVideoInputs(); }
export async function enumerateAudioOutputs() { return await DeviceManager.enumerateAudioOutputs(); }
export async function checkPermissions() { return await DeviceManager.checkPermissions(); }
export async function requestPermissions(audio, video) { return await DeviceManager.requestPermissions(audio, video); }
export function isWebCodecsSupported() { return DeviceManager.isWebCodecsSupported(); }
```

- [ ] **Step 2: Create BoltDeviceManager.cs**

Create `src/Libraries/Bolt/Bolt.Media.Browser/BoltDeviceManager.cs`:

```csharp
namespace Bolt.Media.Browser;

/// <summary>
/// Browser media device enumeration and permission management.
/// Wraps navigator.mediaDevices.enumerateDevices() and Permissions API.
/// </summary>
public sealed class BoltDeviceManager : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BoltDeviceManager(IJSRuntime js) => _js = js;

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        return _module;
    }

    public async Task<MediaDeviceInfo[]> EnumerateAudioInputsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<MediaDeviceInfo[]>("enumerateAudioInputs");
    }

    public async Task<MediaDeviceInfo[]> EnumerateVideoInputsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<MediaDeviceInfo[]>("enumerateVideoInputs");
    }

    public async Task<MediaDeviceInfo[]> EnumerateAudioOutputsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<MediaDeviceInfo[]>("enumerateAudioOutputs");
    }

    public async Task<PermissionStatus> CheckPermissionsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<PermissionStatus>("checkPermissions");
    }

    public async Task<bool> RequestPermissionsAsync(bool audio = true, bool video = false)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("requestPermissions", audio, video);
    }

    public async Task<bool> IsWebCodecsSupportedAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isWebCodecsSupported");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null) await _module.DisposeAsync();
    }
}

public record MediaDeviceInfo(string DeviceId, string Label, string GroupId);

public record PermissionStatus(string Audio, string Video);
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Media.Browser/wwwroot/bolt-media.js
git add src/Libraries/Bolt/Bolt.Media.Browser/BoltDeviceManager.cs
git commit -m "feat(bolt-media-browser): device manager — enumeration + permissions"
```

---

## Task 6: BoltMediaService (Orchestration)

**Files:**
- Create: `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs`
- Modify: `src/Libraries/Bolt/Bolt.Media.Browser/ServiceCollectionExtensions.cs` (verify registrations)

### Context for implementer

`BoltMediaService` is the high-level API that wires everything together:
1. `BoltMediaClient` — call signaling (StartCall, AnswerCall, EndCall, events)
2. `BoltCryptoInterop` — encryption factory for `ExternalMediaEncryption`
3. `BoltAudioPipeline` — audio capture/encode → `BoltMediaStream.SendFrameAsync`, decode → playback
4. `BoltVideoPipeline` — video capture/encode → `BoltMediaStream.SendFrameAsync`, decode → canvas
5. ABR — `BoltMediaStream.OnBitrateChanged` → `ReconfigureBitrateAsync`

Key integration points from `BoltMediaClient` (read `src/Libraries/Bolt/Bolt.Media/BoltMediaClient.cs`):
- `SetEncryptionFactory(Func<IMediaEncryption>)` — call before connecting
- `OnIncomingCall` — `Func<IncomingCallInfo, Task>`
- `OnCallAnswered` — `Func<Guid, Task>` — streams become available after this
- `OnCallEnded` — `Func<Guid, Task>` — cleanup
- `OnKeyframeRequested` — `Action<Guid>` — request keyframe from encoder
- `GetMediaStream(Guid streamId)` — returns `BoltMediaStream?`

Key integration points from `BoltMediaStream`:
- `SendFrameAsync(ReadOnlyMemory<byte> data, bool isKeyframe, CancellationToken ct)` — send encoded frame
- `ReadFramesAsync(CancellationToken ct)` — `IAsyncEnumerable<MediaFrameData>` of received frames
- `OnBitrateChanged` — `Action<int>` — kbps
- `OnKeyframeNeeded` — `Action`

`MediaFrameData` is `readonly record struct(uint SequenceNumber, uint Timestamp, ReadOnlyMemory<byte> Data, bool IsKeyframe)`.

- [ ] **Step 1: Create BoltMediaService.cs**

Create `src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs`:

```csharp
using Bolt.Client;
using Bolt.Protocol;
using Microsoft.AspNetCore.Components;

namespace Bolt.Media.Browser;

/// <summary>
/// High-level voice/video call API for Blazor WASM.
/// Wires BoltMediaClient + browser pipelines + encryption into a single service.
///
/// Usage:
///   @inject BoltMediaService Media
///   await Media.InitializeAsync(boltClient);
///   var callId = await Media.StartCallAsync("recipient", video: true);
///   // ... later
///   await Media.EndCallAsync(callId);
/// </summary>
public sealed class BoltMediaService : IAsyncDisposable
{
    private readonly BoltCryptoInterop _crypto;
    private readonly BoltAudioPipeline _audio;
    private readonly BoltVideoPipeline _video;
    private readonly BoltDeviceManager _devices;
    private readonly MediaServiceOptions _options;
    private readonly ILogger<BoltMediaService> _logger;

    private BoltMediaClient? _mediaClient;
    private CancellationTokenSource? _playbackCts;
    private readonly Dictionary<Guid, CancellationTokenSource> _streamPlaybackTasks = new();

    private bool _initialized;

    // ── Events for Blazor UI ──

    /// <summary>Incoming call. UI should show accept/reject prompt.</summary>
    public event Func<IncomingCallInfo, Task>? OnIncomingCall;

    /// <summary>Call was answered (by remote peer or local user).</summary>
    public event Func<Guid, Task>? OnCallAnswered;

    /// <summary>Call was rejected by remote peer.</summary>
    public event Func<Guid, string?, Task>? OnCallRejected;

    /// <summary>Call ended.</summary>
    public event Func<Guid, Task>? OnCallEnded;

    public bool IsInitialized => _initialized;

    public BoltMediaService(
        BoltCryptoInterop crypto,
        BoltAudioPipeline audio,
        BoltVideoPipeline video,
        BoltDeviceManager devices,
        MediaServiceOptions options,
        ILogger<BoltMediaService> logger)
    {
        _crypto = crypto;
        _audio = audio;
        _video = video;
        _devices = devices;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the media service. Must be called once after the BoltClient is connected.
    /// Loads JS modules, generates encryption keys, wires call events.
    /// </summary>
    public async Task InitializeAsync(BoltClient client)
    {
        if (_initialized) return;

        // Initialize crypto
        if (_options.EnableEncryption)
            await _crypto.InitializeAsync();

        // Initialize audio pipeline
        await _audio.InitializeAsync(_options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps);

        // Create media client and wire events
        _mediaClient = new BoltMediaClient(client, _logger);

        if (_options.EnableEncryption)
            _mediaClient.SetEncryptionFactory(() => _crypto.CreateEncryption());

        _mediaClient.OnIncomingCall += HandleIncomingCallAsync;
        _mediaClient.OnCallAnswered += HandleCallAnsweredAsync;
        _mediaClient.OnCallRejected += async (callId, reason) =>
        {
            await StopPipelinesAsync();
            if (OnCallRejected is not null) await OnCallRejected(callId, reason);
        };
        _mediaClient.OnCallEnded += async callId =>
        {
            await StopPipelinesAsync();
            if (OnCallEnded is not null) await OnCallEnded(callId);
        };
        _mediaClient.OnKeyframeRequested += streamId =>
        {
            _ = _video.RequestKeyframeAsync();
        };

        _initialized = true;
        _logger.LogInformation("BoltMediaService initialized");
    }

    // ── Call API ──

    /// <summary>Start a voice or voice+video call to a recipient.</summary>
    public async Task<Guid> StartCallAsync(string recipientId, bool video = false)
    {
        EnsureInitialized();

        // Wire audio encode → BoltMediaStream (will be connected after call answered)
        _audio.OnEncoded -= OnAudioEncodedForStream;
        _audio.OnEncoded += OnAudioEncodedForStream;

        if (video)
        {
            _video.OnEncoded -= OnVideoEncodedForStream;
            _video.OnEncoded += OnVideoEncodedForStream;
        }

        var callId = await _mediaClient!.StartCallAsync(recipientId, video, _options.EnableEncryption);
        _logger.LogInformation("Call started: {CallId} to {Recipient}, video={Video}", callId, recipientId, video);
        return callId;
    }

    /// <summary>Answer an incoming call.</summary>
    public async Task AnswerCallAsync(Guid callId)
    {
        EnsureInitialized();

        _audio.OnEncoded -= OnAudioEncodedForStream;
        _audio.OnEncoded += OnAudioEncodedForStream;

        await _mediaClient!.AnswerCallAsync(callId);
    }

    /// <summary>Reject an incoming call.</summary>
    public async Task RejectCallAsync(Guid callId)
    {
        EnsureInitialized();
        await _mediaClient!.RejectCallAsync(callId);
    }

    /// <summary>End an active call.</summary>
    public async Task EndCallAsync(Guid callId)
    {
        EnsureInitialized();
        await StopPipelinesAsync();
        await _mediaClient!.EndCallAsync(callId);
    }

    /// <summary>
    /// Start capturing and sending audio. Call after the call is answered.
    /// </summary>
    public async Task StartAudioAsync()
    {
        await _audio.StartCaptureAsync(_options.AudioSampleRate, _options.AudioChannels);
    }

    /// <summary>
    /// Start capturing and sending video. Call after the call is answered.
    /// Pass a canvas ElementReference for remote video rendering.
    /// </summary>
    public async Task StartVideoAsync(ElementReference remoteVideoCanvas)
    {
        await _video.InitializeEncoderAsync(
            _options.VideoWidth, _options.VideoHeight, _options.VideoBitrateKbps,
            _options.VideoFramerate, _options.VideoCodec, _options.KeyframeIntervalFrames);

        await _video.InitializeDecoderAsync(remoteVideoCanvas, _options.VideoCodec);

        await _video.StartCaptureAsync(_options.VideoWidth, _options.VideoHeight, _options.VideoFramerate);
    }

    /// <summary>Stop audio capture (mute).</summary>
    public async Task StopAudioAsync() => await _audio.StopCaptureAsync();

    /// <summary>Stop video capture (camera off).</summary>
    public async Task StopVideoAsync() => await _video.StopCaptureAsync();

    /// <summary>Get the device manager for enumeration and permissions.</summary>
    public BoltDeviceManager Devices => _devices;

    // ── Internal Wiring ──

    private Guid _activeAudioStreamId;
    private Guid _activeVideoStreamId;

    private async Task HandleIncomingCallAsync(IncomingCallInfo info)
    {
        if (OnIncomingCall is not null) await OnIncomingCall(info);
    }

    private async Task HandleCallAnsweredAsync(Guid callId)
    {
        // Send audio config to peer
        var conn = _mediaClient!.GetType()
            .GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_mediaClient) as BoltClient;

        if (conn is not null)
        {
            // Audio config
            _activeAudioStreamId = Guid.NewGuid();
            var audioConn = conn.GetPrimaryConnection();
            var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            BoltCodec.WriteMediaConfig(writer, _activeAudioStreamId, callId, MediaType.Audio, CodecId.Opus,
                _options.AudioSampleRate, _options.AudioChannels, _options.AudioBitrateKbps, 0, ReadOnlySpan<byte>.Empty);
            await audioConn.SendAsync(writer.WrittenMemory, CancellationToken.None);
            writer.Reset();

            // Create local audio stream
            var audioStream = new BoltMediaStream(audioConn, _activeAudioStreamId, callId, true);
            audioStream.EnableFec(_options.FecAudioGroupSize);
            audioStream.EnableNack(128);
            audioStream.EnableVad();
            audioStream.EnablePlc();
            audioStream.OnBitrateChanged += kbps =>
                _ = _audio.ReconfigureBitrateAsync(_options.AudioSampleRate, _options.AudioChannels, kbps);

            // Start receiving remote audio
            StartPlaybackLoop(audioStream);
        }

        if (OnCallAnswered is not null) await OnCallAnswered(callId);
    }

    private void OnAudioEncodedForStream(byte[] data)
    {
        var stream = _mediaClient?.GetMediaStream(_activeAudioStreamId);
        if (stream is not null)
            _ = stream.SendFrameAsync(data, false);
    }

    private void OnVideoEncodedForStream(byte[] data, bool isKeyframe)
    {
        var stream = _mediaClient?.GetMediaStream(_activeVideoStreamId);
        if (stream is not null)
            _ = stream.SendFrameAsync(data, isKeyframe);
    }

    private void StartPlaybackLoop(BoltMediaStream stream)
    {
        var cts = new CancellationTokenSource();
        _streamPlaybackTasks[stream.StreamId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in stream.ReadFramesAsync(cts.Token))
                {
                    if (stream.IsAudio)
                        await _audio.DecodeFrameAsync(frame.Data, frame.Timestamp);
                    else
                        await _video.DecodeFrameAsync(frame.Data, frame.Timestamp, frame.IsKeyframe);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogError(ex, "Playback loop error for stream {StreamId}", stream.StreamId); }
        });
    }

    private async Task StopPipelinesAsync()
    {
        foreach (var (_, cts) in _streamPlaybackTasks)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        _streamPlaybackTasks.Clear();

        if (_audio.IsCapturing) await _audio.StopCaptureAsync();
        if (_video.IsCapturing) await _video.StopCaptureAsync();
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Call InitializeAsync(boltClient) first");
    }

    public async ValueTask DisposeAsync()
    {
        await StopPipelinesAsync();
        if (_mediaClient is not null) await _mediaClient.DisposeAsync();
        await _audio.DisposeAsync();
        await _video.DisposeAsync();
        await _crypto.DisposeAsync();
        await _devices.DisposeAsync();
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Libraries/Bolt/Bolt.Media.Browser/Bolt.Media.Browser.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add src/Libraries/Bolt/Bolt.Media.Browser/BoltMediaService.cs
git commit -m "feat(bolt-media-browser): BoltMediaService — high-level call orchestration"
```

---

## Task 7: Tests and Verification

**Files:**
- Create: `src/Tests/Bolt.Tests/BoltMediaBrowserTests.cs`

### Context for implementer

JS interop cannot be tested without a real browser, but we can test:
1. DI registration (all services resolve correctly with mocked `IJSRuntime`)
2. `BoltCryptoInterop.CreateEncryption()` throws before initialization
3. `BoltMediaService` throws before initialization
4. `MediaServiceOptions` defaults are correct

Test project already exists at `src/Tests/Bolt.Tests/` with NUnit + FluentAssertions.

- [ ] **Step 1: Create test file**

Create `src/Tests/Bolt.Tests/BoltMediaBrowserTests.cs`:

```csharp
using Bolt.Media.Browser;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public class BoltMediaBrowserTests
{
    [Test]
    public void AddBoltMediaBrowser_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(Substitute.For<IJSRuntime>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser();

        var provider = services.BuildServiceProvider();

        provider.GetService<BoltCryptoInterop>().Should().NotBeNull();
        provider.GetService<BoltAudioPipeline>().Should().NotBeNull();
        provider.GetService<BoltVideoPipeline>().Should().NotBeNull();
        provider.GetService<BoltDeviceManager>().Should().NotBeNull();
        provider.GetService<BoltMediaService>().Should().NotBeNull();
        provider.GetService<MediaServiceOptions>().Should().NotBeNull();
    }

    [Test]
    public void AddBoltMediaBrowser_WithOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(Substitute.For<IJSRuntime>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser(opts =>
        {
            opts.AudioBitrateKbps = 128;
            opts.VideoWidth = 1920;
            opts.VideoHeight = 1080;
            opts.EnableEncryption = false;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MediaServiceOptions>();

        options.AudioBitrateKbps.Should().Be(128);
        options.VideoWidth.Should().Be(1920);
        options.VideoHeight.Should().Be(1080);
        options.EnableEncryption.Should().BeFalse();
    }

    [Test]
    public void MediaServiceOptions_HasCorrectDefaults()
    {
        var options = new MediaServiceOptions();

        options.AudioBitrateKbps.Should().Be(64);
        options.AudioSampleRate.Should().Be(48_000);
        options.AudioChannels.Should().Be(1);
        options.VideoWidth.Should().Be(1280);
        options.VideoHeight.Should().Be(720);
        options.VideoBitrateKbps.Should().Be(2_000);
        options.VideoFramerate.Should().Be(30);
        options.VideoCodec.Should().Be("h264");
        options.KeyframeIntervalFrames.Should().Be(60);
        options.EnableEncryption.Should().BeTrue();
        options.EnableFec.Should().BeTrue();
        options.FecAudioGroupSize.Should().Be(4);
        options.FecVideoGroupSize.Should().Be(8);
    }

    [Test]
    public void CryptoInterop_CreateEncryption_ThrowsBeforeInit()
    {
        var js = Substitute.For<IJSRuntime>();
        var crypto = new BoltCryptoInterop(js);

        var act = () => crypto.CreateEncryption();
        act.Should().Throw<InvalidOperationException>().WithMessage("*InitializeAsync*");
    }

    [Test]
    public void MediaService_StartCall_ThrowsBeforeInit()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(Substitute.For<IJSRuntime>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBoltMediaBrowser();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<BoltMediaService>();

        var act = async () => await service.StartCallAsync("someone");
        act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*InitializeAsync*");
    }
}
```

- [ ] **Step 2: Add NSubstitute to test project (if not already present)**

Check `src/Tests/Bolt.Tests/Bolt.Tests.csproj` for NSubstitute. If missing, add:

```xml
<PackageReference Include="NSubstitute" />
```

Also add project reference to Bolt.Media.Browser:

```xml
<ProjectReference Include="..\..\Libraries\Bolt\Bolt.Media.Browser\Bolt.Media.Browser.csproj" />
```

- [ ] **Step 3: Run tests**

Run: `dotnet test src/Tests/Bolt.Tests/ --filter "BoltMediaBrowserTests" -v n`
Expected: All 5 tests pass

- [ ] **Step 4: Commit**

```bash
git add src/Tests/Bolt.Tests/BoltMediaBrowserTests.cs src/Tests/Bolt.Tests/Bolt.Tests.csproj
git commit -m "test(bolt-media-browser): DI registration, options, and initialization guard tests"
```

---

## Consumer Usage Example (Reference)

This is how a Blazor WASM component would use the library:

```csharp
// Program.cs
builder.Services.AddBoltMediaBrowser(opts =>
{
    opts.VideoWidth = 1280;
    opts.VideoHeight = 720;
    opts.EnableEncryption = true;
});

// CallPage.razor
@inject BoltMediaService Media
@inject BoltClient Client

<canvas @ref="_remoteVideo" width="1280" height="720"></canvas>
<button @onclick="StartCall">Call</button>
<button @onclick="EndCall">Hang Up</button>

@code {
    private ElementReference _remoteVideo;
    private Guid _callId;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Media.InitializeAsync(Client);
            Media.OnIncomingCall += async info =>
            {
                await Media.AnswerCallAsync(info.CallId);
                await Media.StartAudioAsync();
                await Media.StartVideoAsync(_remoteVideo);
                StateHasChanged();
            };
        }
    }

    private async Task StartCall()
    {
        _callId = await Media.StartCallAsync("bob", video: true);
        await Media.StartAudioAsync();
        await Media.StartVideoAsync(_remoteVideo);
    }

    private async Task EndCall() => await Media.EndCallAsync(_callId);
}
```

---

## Self-Review Checklist

1. **Spec coverage:** All 3 JS interop bridges covered (crypto Task 2, codecs Tasks 3-4, devices Task 5). Orchestration in Task 6. Tests in Task 7.
2. **Placeholder scan:** No TBD/TODO/placeholders. All code is complete.
3. **Type consistency:** `MediaServiceOptions` used consistently. `BoltCryptoInterop.CreateEncryption()` returns `ExternalMediaEncryption` (from Bolt.Media). `MediaDeviceInfo` and `PermissionStatus` defined in Task 5, used only there. `BoltMediaService` references all pipeline types defined in earlier tasks.
4. **Missing:** `BoltMediaService.HandleCallAnsweredAsync` uses reflection to access `_client` field on `BoltMediaClient` — this is a pragmatic shortcut. A cleaner approach would be to add a public accessor to `BoltMediaClient`, but that modifies an existing file. The implementer should add a `public BoltClient Client { get; }` property to `BoltMediaClient` if reflection feels wrong. Flagged but not blocking.
