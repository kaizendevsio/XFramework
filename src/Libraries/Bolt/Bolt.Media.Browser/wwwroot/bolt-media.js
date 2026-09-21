// Bolt Media — WebCodecs + Media Capture + Playback
// Audio and Video pipelines for Blazor WASM interop

// ─── Audio Pipeline ─────────────────────────────────

class AudioPipeline {
    constructor() {
        this.encoder = null;
        this.receivers = new Map();
        this.decoderConfig = null;
        this.audioContext = null;
        this.mediaStream = null;
        this.captureNode = null;
        this.sourceNode = null;
        this.dotNetRef = null;
        this.captureRunning = false;
        this.transmitting = false;
        this.captureGeneration = 0;
        this.encodedQueue = [];
        this.sending = false;
        this.sources = new Set();
        this.playbackEnabled = true;
        this.managed = false;
        this.resumePending = null;
        this.lastResumeAttempt = -Infinity;
        this.resumeOnInteraction = () => { if (this.playbackEnabled) void this.resumePlayback(); };
        this.visibilityChanged = () => { if (!globalThis.document?.hidden) this.resumeOnInteraction(); };
        globalThis.document?.addEventListener('pointerdown', this.resumeOnInteraction, true);
        globalThis.document?.addEventListener('keydown', this.resumeOnInteraction, true);
        globalThis.document?.addEventListener('visibilitychange', this.visibilityChanged);
        this.outputChanged = () => { void this.getAudioOutputs().then(() => this.resumePlayback()).catch(() => {}); };
        navigator.mediaDevices?.addEventListener?.('devicechange', this.outputChanged);
    }

    createPlaybackContext(sampleRate) {
        if (this.audioContext) return;
        const context = this.audioContext = new AudioContext({ sampleRate, latencyHint: 'interactive' });
        context.onstatechange = () => {
            if (context !== this.audioContext) return;
            globalThis.window?.yap?.diagnostics?.record('audio.state', { phase: context.state });
            if (context.state !== 'running' && this.playbackEnabled) void this.resumePlayback(false);
        };
    }

    getPlaybackState() { return this.audioContext?.state ?? 'closed'; }

    async resumePlayback(userAction = true) {
        const context = this.audioContext;
        if (!context || context.state === 'closed' || !this.playbackEnabled) return false;
        if (context.state === 'running') return true;
        if (this.resumePending) {
            // Chrome can leave an autoplay-blocked resume promise pending. A new
            // user gesture must still call resume synchronously to unlock it.
            if (userAction) void context.resume().catch(() => {});
            return this.resumePending;
        }
        if (!userAction && Date.now() - this.lastResumeAttempt < 1000) return false;
        this.lastResumeAttempt = Date.now();
        this.resumePending = (async () => {
            try { await context.resume(); return context === this.audioContext && context.state === 'running'; }
            catch { return false; } // The call UI offers a gesture if autoplay blocks recovery.
            finally { this.resumePending = null; }
        })();
        return this.resumePending;
    }

    async getAudioOutputs() {
        const context = this.audioContext;
        if (!context?.setSinkId || !navigator.mediaDevices?.enumerateDevices)
            return { supported: false, deviceId: '', devices: [] };
        const devices = [{ id: '', label: 'System default' }];
        for (const device of await navigator.mediaDevices.enumerateDevices()) {
            if (device.kind === 'audiooutput' && device.deviceId && device.deviceId !== 'default' &&
                device.label && !devices.some(item => item.id === device.deviceId))
                devices.push({ id: device.deviceId, label: device.label });
        }
        if (context !== this.audioContext) return { supported: false, deviceId: '', devices: [] };
        // An unplugged headset must not leave a call routed to a missing device.
        if (context.sinkId && !devices.some(device => device.id === context.sinkId)) await context.setSinkId('');
        return { supported: true, deviceId: context.sinkId || '', devices };
    }

    async setAudioOutput(deviceId) {
        const context = this.audioContext;
        if (!context?.setSinkId) throw new Error('Audio output is controlled by this device.');
        await context.setSinkId(deviceId);
        await this.resumePlayback();
        return await this.getAudioOutputs();
    }

    async initEncoder(sampleRate, channels, bitrate) {
        if (sampleRate !== 48000 || channels !== 1)
            throw new Error('Bolt browser voice currently requires 48 kHz mono audio.');
        if (typeof AudioEncoder === 'undefined' || typeof AudioDecoder === 'undefined')
            throw new Error('This browser does not support the audio codecs required for Bolt voice calls.');
        const config = { codec: 'opus', sampleRate, numberOfChannels: channels, bitrate: bitrate * 1000 };
        if (!(await AudioEncoder.isConfigSupported(config)).supported)
            throw new Error('Opus audio encoding is unavailable in this browser.');
        this.encoder?.close();
        this.encoder = new AudioEncoder({
            output: chunk => {
                if (!this.captureRunning || !this.transmitting || !this.dotNetRef) return;
                const data = new Uint8Array(chunk.byteLength);
                chunk.copyTo(data);
                if (this.encodedQueue.length >= 4) this.encodedQueue.shift();
                this.encodedQueue.push(data);
                void this._sendEncoded();
            },
            error: e => { console.error('Bolt audio encoder:', e); this.stopCapture(); }
        });
        this.encoder.configure(config);
    }

    async _sendEncoded() {
        if (this.sending) return;
        this.sending = true;
        try {
            while (this.captureRunning && this.transmitting && this.dotNetRef && this.encodedQueue.length)
                await this.dotNetRef.invokeMethodAsync(this.managed ? 'OnAudioPcm' : 'OnAudioEncoded', this.encodedQueue.shift());
        } catch (error) {
            console.error('Bolt audio send:', error);
            this.stopCapture();
        } finally { this.sending = false; }
    }

    async initDecoder(sampleRate, channels) {
        const config = { codec: 'opus', sampleRate, numberOfChannels: channels };
        if (!(await AudioDecoder.isConfigSupported(config)).supported)
            throw new Error('Opus audio decoding is unavailable in this browser.');
        this.createPlaybackContext(sampleRate);
        this.decoderConfig = config;
    }

    initManaged() {
        this.managed = true;
        this.createPlaybackContext(48000);
    }

    async startCapture(dotNetRef, constraints, transmit = true) {
        if (this.captureRunning) {
            void this.resumePlayback();
            this.setCaptureMuted(!transmit);
            return this.captureRunning;
        }
        if ((!this.encoder && !this.managed) || !this.audioContext?.audioWorklet)
            throw new Error('AudioWorklet capture is unavailable in this browser.');
        const generation = ++this.captureGeneration;
        try {
            // Do not wait for autoplay permission before requesting the microphone:
            // capture permission itself can unlock audio on mobile Chrome.
            void this.resumePlayback();
            const stream = await navigator.mediaDevices.getUserMedia({ audio: {
                sampleRate: 48000, channelCount: 1, echoCancellation: true,
                noiseSuppression: true, autoGainControl: true
            }, video: false });
            if (generation !== this.captureGeneration) {
                stream.getTracks().forEach(track => track.stop());
                return false;
            }
            this.mediaStream = stream;
            await this.audioContext.audioWorklet.addModule(new URL('./bolt-audio-capture.js', import.meta.url));
            // Android may suspend or interrupt output while microphone permission
            // or the communication audio route changes. Resume after that change.
            void this.resumePlayback();
            if (generation !== this.captureGeneration) return false;
            this.sourceNode = this.audioContext.createMediaStreamSource(stream);
            this.captureNode = new AudioWorkletNode(this.audioContext, 'bolt-audio-capture', { numberOfOutputs: 0 });
            this.dotNetRef = dotNetRef;
            this.captureRunning = true;
            this.transmitting = transmit;
            this.playbackEnabled = true;
            const node = this.captureNode;
            node.port.onmessage = ({ data }) => {
                try {
                    // Permission/preparation must not advance codec history before a stream
                    // can send. Likewise skip raw PCM, not encoded packets, under backpressure.
                    if (!this.captureRunning || !this.transmitting || this.sending || this.encodedQueue.length) return;
                    if (this.managed) {
                        const bytes = new Uint8Array(1920);
                        const view = new DataView(bytes.buffer);
                        for (let i = 0; i < 960; i++)
                            view.setInt16(i * 2, Math.round(Math.max(-1, Math.min(1, data.samples[i])) * 32767), true);
                        if (this.encodedQueue.length >= 4) this.encodedQueue.shift();
                        this.encodedQueue.push(bytes);
                        void this._sendEncoded();
                        return;
                    }
                    if (this.encoder?.state !== 'configured' || this.encoder.encodeQueueSize >= 1) return;
                    const audio = new AudioData({ format: 'f32-planar', sampleRate: 48000,
                        numberOfFrames: 960, numberOfChannels: 1, timestamp: data.timestamp, data: data.samples });
                    try { this.encoder.encode(audio); } finally { audio.close(); }
                } finally { node.port.postMessage('ack'); }
            };
            this.sourceNode.connect(node);
            return true;
        } catch (error) {
            if (generation === this.captureGeneration) this.stopCapture();
            throw error;
        }
    }

    setCaptureMuted(muted) {
        this.transmitting = this.captureRunning && !muted;
        this.encodedQueue.length = 0;
        this.mediaStream?.getAudioTracks().forEach(track => { track.enabled = !muted; });
    }

    stopCapture() {
        this.captureGeneration++;
        this.captureRunning = false;
        this.transmitting = false;
        this.dotNetRef = null;
        this.encodedQueue.length = 0;
        this.captureNode?.disconnect();
        if (this.captureNode) { this.captureNode.port.onmessage = null; this.captureNode.port.close(); }
        this.sourceNode?.disconnect();
        this.captureNode = this.sourceNode = null;
        this.mediaStream?.getTracks().forEach(track => track.stop());
        this.mediaStream = null;
    }

    stopPlayback() {
        this.playbackEnabled = false;
        for (const streamId of [...this.receivers.keys()]) this.removeRemoteStream(streamId);
    }

    receiver(streamId) {
        if (this.receivers.has(streamId)) return this.receivers.get(streamId);
        if (!this.playbackEnabled || this.receivers.size >= 8) return null;
        const receiver = { decoder: null, sources: new Set(), nextPlayTime: 0, active: true };
        if (!this.managed) {
            if (!this.decoderConfig) return null;
            receiver.decoder = new AudioDecoder({
                output: data => this._playAudioData(data, streamId, receiver),
                error: e => console.error('Bolt audio decoder:', e)
            });
            receiver.decoder.configure(this.decoderConfig);
        }
        this.receivers.set(streamId, receiver);
        return receiver;
    }

    removeRemoteStream(streamId) {
        const receiver = this.receivers.get(streamId);
        if (!receiver) return;
        receiver.active = false;
        if (receiver.decoder?.state !== 'closed') receiver.decoder?.close();
        for (const source of receiver.sources) {
            source.stop(); source.disconnect(); this.sources.delete(source);
        }
        receiver.sources.clear();
        this.receivers.delete(streamId);
    }

    decodeFrame(data, timestamp, streamId = 'default') {
        if (!this.playbackEnabled) return;
        const decoder = this.receiver(streamId)?.decoder;
        if (decoder?.state !== 'configured' || decoder.decodeQueueSize >= 8) return;
        // Bolt audio timestamps use a 48 kHz clock; WebCodecs expects microseconds.
        decoder.decode(new EncodedAudioChunk({ type: 'key', timestamp: Math.round(timestamp * 1000000 / 48000), data }));
    }

    reconfigureBitrate(sampleRate, channels, bitrate) {
        if (this.encoder?.state === 'configured')
            this.encoder.configure({ codec: 'opus', sampleRate, numberOfChannels: channels, bitrate: bitrate * 1000 });
    }

    _playAudioData(data, streamId = 'default', receiver = this.receiver(streamId)) {
        try {
            const context = this.audioContext;
            if (context && context.state !== 'running' && this.playbackEnabled) { void this.resumePlayback(false); return; }
            // Do not accumulate delayed playback during suspension or a slow renderer.
            if (!this.playbackEnabled || !receiver?.active || context?.state !== 'running' ||
                receiver.nextPlayTime - context.currentTime > 0.2 || receiver.sources.size >= 16) return;
            const buffer = context.createBuffer(data.numberOfChannels, data.numberOfFrames, data.sampleRate);
            for (let ch = 0; ch < data.numberOfChannels; ch++) {
                const samples = new Float32Array(data.numberOfFrames);
                data.copyTo(samples, { planeIndex: ch, format: 'f32-planar' });
                buffer.copyToChannel(samples, ch);
            }
            const source = context.createBufferSource();
            source.buffer = buffer;
            source.connect(context.destination);
            source.onended = () => { this.sources.delete(source); receiver.sources.delete(source); source.disconnect(); };
            this.sources.add(source);
            receiver.sources.add(source);
            receiver.nextPlayTime = Math.max(receiver.nextPlayTime, context.currentTime);
            source.start(receiver.nextPlayTime);
            receiver.nextPlayTime += buffer.duration;
        } finally { data.close(); }
    }

    playPcm(bytes, streamId = 'default') {
        if (bytes.length !== 1920) return;
        const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        this._playAudioData({ numberOfFrames: 960, numberOfChannels: 1, sampleRate: 48000,
            copyTo: samples => { for (let i = 0; i < 960; i++) samples[i] = view.getInt16(i * 2, true) / 32768; },
            close() {} }, streamId);
    }

    async dispose() {
        globalThis.document?.removeEventListener('pointerdown', this.resumeOnInteraction, true);
        globalThis.document?.removeEventListener('keydown', this.resumeOnInteraction, true);
        globalThis.document?.removeEventListener('visibilitychange', this.visibilityChanged);
        navigator.mediaDevices?.removeEventListener?.('devicechange', this.outputChanged);
        this.stopCapture();
        this.stopPlayback();
        if (this.encoder?.state !== 'closed') this.encoder?.close();
        if (this.audioContext) this.audioContext.onstatechange = null;
        if (this.audioContext?.state !== 'closed') await this.audioContext?.close();
        this.encoder = this.audioContext = null;
    }
}

// ─── Video ──────────────────────────────────────────
// Codec strings are chosen per height: a level that is too low is rejected outright by
// isConfigSupported, and one that is too high needlessly excludes small hardware encoders.

function av1Codec(height, fps = 30) { if (height > 1080) return fps > 30 ? 'av01.0.13M.08' : 'av01.0.12M.08'; return height > 720 ? (fps > 30 ? 'av01.0.09M.08' : 'av01.0.08M.08') : height > 480 ? (fps > 30 ? 'av01.0.08M.08' : 'av01.0.05M.08') : 'av01.0.04M.08'; }
function vp9Codec(height, fps = 30) { return height > 1080 ? (fps > 30 ? 'vp09.00.51.08' : 'vp09.00.50.08') : height > 720 ? (fps > 30 ? 'vp09.00.41.08' : 'vp09.00.40.08') : height > 480 ? (fps > 30 ? 'vp09.00.40.08' : 'vp09.00.31.08') : 'vp09.00.21.08'; }
function h264Codec(height, fps = 30) { return height > 1080 ? (fps > 30 ? 'avc1.640034' : 'avc1.640033') : height > 720 ? (fps > 30 ? 'avc1.4d002a' : 'avc1.4d0028') : height > 480 ? (fps > 30 ? 'avc1.420020' : 'avc1.42001f') : 'avc1.42001e'; }

export function videoCodecString(codec, height, fps = 30) {
    return codec === 'av1' ? av1Codec(height, fps) : codec === 'vp9' ? vp9Codec(height, fps) : h264Codec(height, fps);
}

export function h264BitstreamCodec(data) {
    // Annex B SPS starts near the front of every keyframe. Bound the scan of untrusted media.
    for (let i = 0; i + 7 < Math.min(data.length, 65536); i++) {
        if (data[i] !== 0 || data[i + 1] !== 0) continue;
        const nal = data[i + 2] === 1 ? i + 3 : data[i + 2] === 0 && data[i + 3] === 1 ? i + 4 : -1;
        if (nal < 0 || (data[nal] & 31) !== 7) continue;
        return 'avc1.' + [...data.slice(nal + 1, nal + 4)].map(x => x.toString(16).padStart(2, '0')).join('');
    }
    return null;
}

function encoderConfig(codec, width, height, bitrateKbps, framerate, hardware) {
    const config = {
        codec: videoCodecString(codec, Math.min(width, height), framerate), width, height,
        bitrate: Math.max(64, bitrateKbps) * 1000, framerate,
        latencyMode: 'realtime', bitrateMode: 'variable', scalabilityMode: 'L1T1',
        hardwareAcceleration: hardware ?? 'no-preference'
    };
    // Without a decoder description an H.264/H.265 bitstream has to be Annex B, and the
    // decoder here is fed raw chunks. Asking for the wrong container plays as green mush.
    // WebKit rejects odd H.264 dimensions outright, so both sides are rounded to even.
    if (codec === 'h264') { config.avc = { format: 'annexb' }; config.width &= ~1; config.height &= ~1; }
    return config;
}

/// Reshape a ladder tier to the camera's own aspect ratio.
///
/// A tier is a bitrate budget written as a landscape raster (1280x720 and so on). VideoEncoder does
/// not letterbox a frame that does not match its configured size - it scales it to fit, and scaling
/// a 720x1280 portrait phone frame into 1280x720 squashes the picture. The far side then receives a
/// genuinely stretched 16:9 raster and letterboxes *that*: a stretched face between black bands.
/// Keep the tier's short edge as the quality knob and let the camera decide the long one.
export function fitTierToSource(width, height, sourceWidth, sourceHeight) {
    if (!(sourceWidth > 0) || !(sourceHeight > 0)) return { width, height };
    // A camera that only supplies 720p must not be upscaled to a 4K preference.
    const across = Math.min(width, height, sourceWidth, sourceHeight), longest = Math.max(width, height);
    const portrait = sourceHeight > sourceWidth;
    const ratio = portrait ? sourceHeight / sourceWidth : sourceWidth / sourceHeight;
    // Never past the tier's long edge: that length is what the chosen bitrate was measured against.
    const along = Math.min(longest, Math.round(across * ratio));
    return portrait ? { width: across, height: along } : { width: along, height: across };
}

/// How this browser can get camera frames out of a MediaStreamTrack.
///
/// Chromium exposes MediaStreamTrackProcessor on Window. Safari 18 has it too but only inside a
/// DedicatedWorker, so on the page it is undefined there and in Firefox. Both reach the same
/// frames through requestVideoFrameCallback on a <video> element (Safari 15.4+, WebCodecs 16.4+).
/// Feature detection of the primitive itself, never a user-agent guess.
export function videoCaptureStrategy() {
    if (typeof MediaStreamTrackProcessor !== 'undefined') return 'processor';
    if (typeof VideoFrame !== 'undefined' && typeof HTMLVideoElement !== 'undefined' &&
        typeof HTMLVideoElement.prototype?.requestVideoFrameCallback === 'function') return 'rvfc';
    return 'none';
}

/// Probe what this device can actually do, per codec, encode and decode, hardware and software.
/// Everything below is decided from these answers rather than from a user-agent guess.
///
/// The gate asks only for the codec APIs being probed. Requiring MediaStreamTrackProcessor here
/// handed Safari an empty ladder, which surfaced as "this device cannot encode video" on hardware
/// that encodes H.264 perfectly well; capture support is a separate question, asked separately.
export async function probeVideoCodecs(maxHeight = 2160) {
    const results = [];
    if (typeof VideoEncoder === 'undefined' || typeof VideoDecoder === 'undefined') return results;
    const heights = [2160, 1440, 1080, 720, 540, 360].filter(h => h <= maxHeight);
    if (heights.length === 0) heights.push(Math.max(180, maxHeight));
    for (const codec of ['av1', 'vp9', 'h264']) {
        let best = 0, hardware = false, decode = false, decodeMaxHeight = 0;
        for (const height of heights) {
            const width = Math.round(height * 16 / 9 / 2) * 2;
            try {
                const supported = await VideoEncoder.isConfigSupported(encoderConfig(codec, width, height, 2000, 30));
                if (!supported?.supported) continue;
                if (best === 0) best = height;
                // WebCodecs has no require-hardware mode. prefer-hardware is only a hint,
                // so isConfigSupported cannot prove acceleration; retain conservative limits.
            } catch { /* An unsupported configuration is an answer, not a failure. */ }
        }
        for (const height of heights) {
            try {
                const supported = await VideoDecoder.isConfigSupported(
                    { codec: videoCodecString(codec, height), codedWidth: Math.round(height * 16 / 9), codedHeight: height, hardwareAcceleration: 'no-preference' });
                if (supported?.supported) { decode = true; decodeMaxHeight = height; break; }
            } catch { /* Same: treat a throw as unsupported. */ }
        }
        results.push({ codec, encode: best > 0, decode, hardware, maxHeight: best, decodeMaxHeight });
    }
    return results;
}

/// Battery can lower the ceiling; codec probes and measured pressure decide device capacity.
export async function videoDeviceCeiling() {
    let ceiling = 2160;
    // CPU core count does not describe a phone's dedicated video encoder.
    try {
        const battery = await navigator.getBattery?.();
        // A phone below a fifth of its battery and off the charger should not spend it on pixels
        // nobody can see on a small screen.
        if (battery && !battery.charging && battery.level <= 0.2) ceiling = Math.min(ceiling, 540);
    } catch { /* The Battery API is absent or blocked; the other limits still apply. */ }
    return ceiling;
}

class VideoPipeline {
    constructor() {
        this.encoder = null;
        this.mediaStream = null;
        this.captureReader = null;
        this.captureRunning = false;
        this.captureGeneration = 0;
        // rVFC fallback state: the off-screen element frames are read from, its pending callback
        // handle, and the canvas only used if a browser refuses a VideoFrame built from the element.
        this.captureVideo = null;
        this.captureCallbackId = 0;
        this.captureStrategy = null;
        this.captureCanvas = null;
        this.captureContext = null;
        this.preview = null;
        this.dotNetRef = null;
        this.remotes = new Map();
        this.config = null;
        this.codec = 'h264';
        this.keyframeIntervalMs = 2000;
        this.lastKeyframe = 0;
        this.frameId = 0;
        this.pendingKeyframe = true;
        this.window = { since: 0, frames: 0, bytes: 0 };
        this.stats = { fps: 0, kbps: 0, dropped: 0, backlog: 0 };
        this.facingMode = 'user';
        this.deviceId = '';
        this.lastRequest = null;
        this.lastCaptureTimestamp = null;
        // What the ladder asked for, kept apart from what the encoder is actually configured with:
        // the raster is the tier reshaped to whatever the camera is currently handing us, and that
        // shape changes on a camera switch and when the phone is turned over mid-call.
        this.tier = null;
        this.sourceWidth = 0;
        this.sourceHeight = 0;
        this.hostRef = null;
        this.hidden = () => { if (globalThis.document?.hidden && this.captureRunning) this.stopCapture('hidden'); };
        globalThis.document?.addEventListener('visibilitychange', this.hidden);
        globalThis.addEventListener?.('pagehide', this.hidden);
    }

    async initEncoder(codec, width, height, bitrateKbps, framerate, keyframeSeconds) {
        this.codec = codec || 'h264';
        this.keyframeIntervalMs = Math.max(500, (keyframeSeconds || 2) * 1000);
        this.tier = { width, height, bitrateKbps, framerate };
        ({ width, height } = fitTierToSource(width, height, this.sourceWidth, this.sourceHeight));
        // 'prefer-hardware' is not a hint that degrades gracefully: on a machine with no hardware
        // encoder Chrome reports the configuration unsupported and configure() throws outright.
        // Ask for hardware, then settle for whatever the browser has.
        let config = null;
        for (const acceleration of ['prefer-hardware', 'no-preference']) {
            const candidate = encoderConfig(this.codec, width, height, bitrateKbps, framerate, acceleration);
            try { if ((await VideoEncoder.isConfigSupported(candidate))?.supported) { config = candidate; break; } }
            catch { /* Treat a throw as unsupported and try the next acceleration. */ }
        }
        if (!config) throw new Error('This device cannot encode video for calls.');
        this._closeEncoder();
        this.config = config;
        // The published stream survives camera off/on; its frame IDs must too.
        this.pendingKeyframe = true;
        this.encoder = new VideoEncoder({
            output: (chunk) => this._onEncoded(chunk),
            error: (error) => {
                console.error('Bolt video encoder:', error);
                this.stopCapture('encoder');
            }
        });
        this.encoder.configure(config);
        return { width: config.width, height: config.height, codec: config.codec };
    }

    _onEncoded(chunk) {
        const data = new Uint8Array(chunk.byteLength);
        chunk.copyTo(data);
        const now = Date.now();
        if (this.window.since === 0) this.window.since = now;
        this.window.frames++; this.window.bytes += data.byteLength;
        const elapsed = now - this.window.since;
        if (elapsed >= 1000) {
            this.stats.fps = this.window.frames * 1000 / elapsed;
            this.stats.kbps = this.window.bytes * 8 / elapsed;
            this.window = { since: now, frames: 0, bytes: 0 };
        }
        if (!this.captureRunning || !this.dotNetRef) return;
        // Frame IDs are the reassembly key on the far side; they must not restart mid-call.
        const frameId = (this.frameId = (this.frameId + 1) >>> 0);
        void this.dotNetRef.invokeMethodAsync('OnVideoEncoded', data, chunk.type === 'key',
            frameId, Math.max(0, Math.round(chunk.timestamp)) >>> 0);
    }

    /// Opening the camera is the only place getUserMedia is called with video, and it happens
    /// only from an explicit user action on the call screen.
    async startCapture(dotNetRef, options) {
        if (!this.encoder) throw new Error('Configure the video encoder first.');
        // A bandwidth-driven resume passes nothing; it must come back on the camera the user chose.
        const wanted = (options && (options.deviceId || options.facingMode)) ? options : (this.lastRequest || {});
        const same = this.lastRequest && wanted.deviceId === this.lastRequest.deviceId &&
            wanted.facingMode === this.lastRequest.facingMode;
        if (this.captureRunning && same) return this.describe();
        // Switching between the front and back camera means closing the one that is open: a phone
        // will not hand out both at once, and leaving the old track live keeps its indicator lit.
        if (this.captureRunning) this.stopCapture();
        this.lastRequest = wanted;
        const generation = ++this.captureGeneration;
        const video = {
            width: { ideal: this.config.width }, height: { ideal: this.config.height },
            frameRate: { ideal: this.config.framerate, max: this.config.framerate }
        };
        if (wanted.deviceId) video.deviceId = { exact: wanted.deviceId };
        else if (wanted.facingMode) video.facingMode = { ideal: wanted.facingMode };
        let stream;
        try { stream = await navigator.mediaDevices.getUserMedia({ audio: false, video }); }
        catch (error) {
            if (generation === this.captureGeneration) this.stopCapture('denied');
            throw error;
        }
        if (generation !== this.captureGeneration) { stream.getTracks().forEach(t => t.stop()); return this.describe(); }
        const track = stream.getVideoTracks()[0];
        if (!track) { stream.getTracks().forEach(t => t.stop()); throw new Error('No camera was available.'); }
        this.mediaStream = stream;
        const settings = track.getSettings?.() ?? {};
        this.deviceId = settings.deviceId || wanted.deviceId || '';
        this.facingMode = settings.facingMode || wanted.facingMode || 'user';
        this.captureRunning = true;
        this.pendingKeyframe = true;
        this.window = { since: 0, frames: 0, bytes: 0 };
        this.lastCaptureTimestamp = null;
        this.dotNetRef = dotNetRef;
        if (this.preview) this.preview.srcObject = stream;
        // The camera can be revoked from the browser's own UI; that must end the send, not hang it.
        track.addEventListener('ended', () => { if (generation === this.captureGeneration) this.stopCapture('ended'); });
        // Everything downstream of here - encode, fragmentation, SFrame, adaptation, transport - is
        // identical on both strategies; only the way a VideoFrame is obtained differs.
        if (this.strategy() === 'processor') {
            this.captureReader = new MediaStreamTrackProcessor({ track }).readable.getReader();
            void this._readLoop(generation);
        } else {
            this._startFrameCallbacks(stream, generation);
        }
        return this.describe();
    }

    strategy() { return this.captureStrategy ?? videoCaptureStrategy(); }

    /// Force a capture strategy. Only the tests and the cost measurement use this; a real page
    /// picks by feature detection, because a browser that has both should use the cheaper one.
    useCaptureStrategy(strategy) { this.captureStrategy = strategy || null; }

    /// The Safari path: an off-screen <video> carrying the camera stream, one callback per decoded
    /// frame, painted at the encoder size so sensor rotation cannot leak into the bitstream.
    _startFrameCallbacks(stream, generation) {
        const video = this.captureVideo = globalThis.document.createElement('video');
        video.srcObject = stream;
        video.muted = video.defaultMuted = true;
        video.autoplay = video.playsInline = true;
        video.setAttribute('playsinline', '');
        // iOS only keeps decoding for an element the page actually has: a detached or display:none
        // video is allowed to stall, and a stalled element never fires the frame callback.
        video.style.cssText = 'position:fixed;top:0;left:0;width:1px;height:1px;opacity:0;pointer-events:none';
        globalThis.document.body?.appendChild(video);
        const onFrame = (_now, metadata) => {
            this.captureCallbackId = 0;
            // A callback queued before stopCapture must not push a frame from the previous session.
            if (!this.captureRunning || generation !== this.captureGeneration) return;
            try { this._encodeElementFrame(video, metadata); }
            finally {
                // Re-arm in finally: one bad frame must not silently end the whole capture.
                if (this.captureRunning && generation === this.captureGeneration)
                    this.captureCallbackId = video.requestVideoFrameCallback(onFrame);
            }
        };
        this.captureCallbackId = video.requestVideoFrameCallback(onFrame);
        void video.play?.()?.catch?.(error => {
            // A muted element fed by getUserMedia is exempt from autoplay rules everywhere this
            // path runs, so a refusal means no frame will ever arrive: end it, don't send black.
            console.error('Bolt video capture element:', error);
            if (generation === this.captureGeneration) this.stopCapture('capture');
        });
    }

    /// One VideoFrame per callback, closed on every path. The fallback allocates a frame per
    /// picture, so a single missed close() exhausts the frame pool within seconds.
    _encodeElementFrame(video, metadata) {
        if (!this.encoder || this.encoder.state !== 'configured') return;
        // Same rule as the reader loop: drop the newest frame rather than queue latency.
        if (this.encoder.encodeQueueSize >= 2) { this.stats.dropped++; return; }
        // Before the frame is built, so a reconfigure here can never strand one unclosed. No
        // _upright on this path: the element paints the rotation itself, so it is already upright.
        this._noteSource(video.videoWidth, video.videoHeight);
        const seconds = metadata?.mediaTime ?? video.currentTime ?? 0;
        const timestamp = Math.max(0, Math.round(seconds * 1e6));
        if (!this._acceptCaptureTime(timestamp)) return;
        const frame = this._elementFrame(video, timestamp);
        if (!frame) return;
        try {
            const now = Date.now();
            const keyFrame = this.pendingKeyframe || now - this.lastKeyframe >= this.keyframeIntervalMs;
            if (keyFrame) { this.lastKeyframe = now; this.pendingKeyframe = false; }
            this.encoder.encode(frame, { keyFrame });
        } finally { frame.close(); }
    }

    /// A video-element VideoFrame can retain sensor rotation, including on Safari. Paint the
    /// displayed image into a reusable, encoder-sized canvas to bake orientation into the pixels.
    /// No getImageData/readback and no full-resolution intermediate allocation.
    _elementFrame(video, timestamp) {
        // WebKit throws InvalidStateError for an element below HAVE_CURRENT_DATA or with no decoded
        // frame in hand. rVFC should never hand us one, but a throw per frame would be expensive.
        if (!video.videoWidth || !video.videoHeight || (video.readyState ?? 2) < 2) return null;
        try {
            const canvas = this.captureCanvas ??= typeof OffscreenCanvas !== 'undefined'
                ? new OffscreenCanvas(this.config.width, this.config.height)
                : globalThis.document.createElement('canvas');
            if (canvas.width !== this.config.width || canvas.height !== this.config.height)
            { canvas.width = this.config.width; canvas.height = this.config.height; }
            this.captureContext ??= canvas.getContext('2d', { alpha: false, desynchronized: true });
            this.captureContext.drawImage(video, 0, 0, canvas.width, canvas.height);
            return new VideoFrame(canvas, { timestamp });
        } catch { return null; }
    }

    /// rVFC hands back a handle; an uncancelled one fires after the camera is gone and would build
    /// a frame from a dead element. The canvas goes too: it would otherwise hold the last picture.
    _stopFrameCallbacks() {
        const video = this.captureVideo;
        this.captureVideo = null;
        this.captureCanvas = this.captureContext = null;
        if (!video) return;
        if (this.captureCallbackId) { try { video.cancelVideoFrameCallback(this.captureCallbackId); } catch { } }
        this.captureCallbackId = 0;
        try { video.pause?.(); } catch { }
        video.srcObject = null;
        video.remove?.();
    }

    async _readLoop(generation) {
        while (this.captureRunning && generation === this.captureGeneration) {
            let frame;
            try {
                const { value, done } = await this.captureReader.read();
                if (done) break;
                frame = value;
            } catch { break; }
            try {
                if (!this.encoder || this.encoder.state !== 'configured') continue;
                // Dropping the newest frame beats queueing it: a backlog is latency the call never recovers.
                if (this.encoder.encodeQueueSize >= 2) { this.stats.dropped++; continue; }
                if (!this._acceptCaptureTime(frame.timestamp)) continue;
                frame = this._upright(frame);
                this._noteSource(frame.displayWidth, frame.displayHeight);
                const now = Date.now();
                const keyFrame = this.pendingKeyframe || now - this.lastKeyframe >= this.keyframeIntervalMs;
                if (keyFrame) { this.lastKeyframe = now; this.pendingKeyframe = false; }
                this.encoder.encode(frame, { keyFrame });
            } finally { frame.close(); }
        }
    }

    _acceptCaptureTime(timestamp) {
        const interval = 1e6 / (this.config?.framerate || 30);
        if (this.lastCaptureTimestamp !== null && timestamp >= this.lastCaptureTimestamp &&
            timestamp - this.lastCaptureTimestamp < interval * 0.9) return false;
        this.lastCaptureTimestamp = timestamp;
        return true;
    }

    describe() {
        return { capturing: this.captureRunning, deviceId: this.deviceId, facingMode: this.facingMode,
            width: this.config?.width ?? 0, height: this.config?.height ?? 0, codec: this.codec,
            strategy: this.strategy() };
    }

    getStats() {
        this.stats.backlog = this.encoder?.encodeQueueSize ?? 0;
        return { fps: this.stats.fps, kbps: this.stats.kbps, dropped: this.stats.dropped, backlog: this.stats.backlog };
    }

    attachPreview(element) {
        this.preview = element || null;
        // srcObject shows the camera without a second decode; drawing it would double the cost.
        if (this.preview) this.preview.srcObject = this.mediaStream;
    }

    setPreviewEnabled(enabled) { if (this.preview) this.preview.srcObject = enabled ? this.mediaStream : null; }

    /// Change picture size, rate or bitrate in place. A reconfigure needs a fresh keyframe or the
    /// far side decodes the new size against the old reference.
    async applyTier(width, height, bitrateKbps, framerate) {
        this.tier = { width, height, bitrateKbps, framerate };
        const changed = this._configureForSource();
        const track = this.mediaStream?.getVideoTracks()[0];
        if (changed && track?.applyConstraints) {
            try { await track.applyConstraints({ width: { ideal: width }, height: { ideal: height },
                frameRate: { ideal: framerate, max: framerate } }); }
            catch { /* Keep the working track; encoder pacing still enforces the limit. */ }
        }
        return changed;
    }

    /// Configure the encoder for the current tier at the current camera's aspect ratio. Called when
    /// the ladder moves, and again whenever the frames themselves change shape.
    _configureForSource() {
        if (!this.encoder || this.encoder.state !== 'configured' || !this.config || !this.tier) return false;
        const fitted = fitTierToSource(this.tier.width, this.tier.height, this.sourceWidth, this.sourceHeight);
        const next = encoderConfig(this.codec, fitted.width, fitted.height,
            this.tier.bitrateKbps, this.tier.framerate, this.config.hardwareAcceleration);
        if (next.width === this.config.width && next.height === this.config.height &&
            next.bitrate === this.config.bitrate && next.framerate === this.config.framerate) return false;
        this.config = next;
        this.encoder.configure(next);
        this.pendingKeyframe = true;
        return true;
    }

    requestKeyframe() { this.pendingKeyframe = true; }

    /// Record the shape of the frames now arriving and re-fit the encoder if it changed. This is
    /// what carries a camera switch (front 4:3 to back 16:9) and a phone turned over mid-call.
    _noteSource(width, height) {
        if (!(width > 0) || !(height > 0) || (width === this.sourceWidth && height === this.sourceHeight)) return;
        this.sourceWidth = width; this.sourceHeight = height;
        this._configureForSource();
    }

    /// Bake a frame's display rotation into its pixels.
    ///
    /// Camera frames can carry orientation metadata that the encoded elementary stream loses.
    /// drawImage applies that metadata once; the resulting canvas frame has upright pixels.
    _upright(frame) {
        const rotation = (((frame.rotation ?? 0) % 360) + 360) % 360;
        const flip = frame.flip === true;
        if (!rotation && !flip) return frame;
        try {
            // displayWidth/Height are the post-rotation dimensions, which is the canvas we want.
            const width = frame.displayWidth, height = frame.displayHeight;
            if (!(width > 0) || !(height > 0)) return frame;
            const canvas = this.rotateCanvas ??= typeof OffscreenCanvas !== 'undefined'
                ? new OffscreenCanvas(width, height)
                : globalThis.document.createElement('canvas');
            if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
            const context = this.rotateContext ??= canvas.getContext('2d', { alpha: false, desynchronized: true });
            if (!context) return frame;
            // drawImage applies the VideoFrame orientation. A clone with rotation: 0 does
            // NOT erase inherited rotation (WebCodecs composes it), so never rotate twice.
            context.drawImage(frame, 0, 0, width, height);
            const upright = new VideoFrame(canvas, { timestamp: frame.timestamp, duration: frame.duration });
            frame.close();
            return upright;
        } catch (error) {
            // A rotated picture beats no picture: fall back to sending the frame as it came.
            console.warn('Bolt video: could not normalise frame orientation.', error);
            return frame;
        }
    }

    /// One decoder and one canvas per remote sender: group tiles are independent, and a stall in
    /// one participant's stream must not freeze the others.
    addRemote(streamId, canvas, codec, hostRef) {
        // The host reference is kept apart from the capture one: a decode failure has to reach
        // C# whether or not this device's own camera is on.
        this.hostRef = hostRef ?? this.hostRef;
        this.removeRemote(streamId);
        if (this.remotes.size >= 7) return false;
        const context = canvas?.getContext?.('2d', { alpha: false, desynchronized: true });
        if (!context) return false;
        const remote = { streamId, canvas, context, codec: codec || 'h264', decoder: null, width: 0, height: 0,
            primed: false, pending: new Map(), lateFrames: 0, software: false, fallbackTried: false };
        this._openDecoder(streamId, remote);
        this.remotes.set(streamId, remote);
        return true;
    }

    _openDecoder(streamId, remote) {
        remote.primed = false;
        remote.pending.clear();
        remote.lateFrames = 0;
        remote.decoder = new VideoDecoder({
            output: (frame) => this._render(remote, frame),
            error: (error) => {
                console.error('Bolt video decoder:', error);
                // A decoder that errored is closed for good. Rebuild it and wait for the next
                // keyframe; the C# side asks the sender for one rather than showing a frozen tile.
                remote.software = false;
                if (this.remotes.get(streamId) === remote) this._openDecoder(streamId, remote);
                void this.hostRef?.invokeMethodAsync('OnVideoDecodeFailed', streamId);
            }
        });
        remote.decoder.configure({ codec: remote.bitstreamCodec || videoCodecString(remote.codec, 1080), hardwareAcceleration: remote.software ? 'prefer-software' : 'no-preference',
            optimizeForLatency: true });
    }

    removeRemote(streamId) {
        const remote = this.remotes.get(streamId);
        if (!remote) return;
        if (remote.decoder?.state !== 'closed') remote.decoder?.close();
        this.remotes.delete(streamId);
    }

    decodeFrame(streamId, data, timestamp, isKeyframe, discontinuity = false) {
        const remote = this.remotes.get(streamId);
        if (!remote || remote.decoder?.state !== 'configured') return false;
        if (isKeyframe && remote.codec === 'h264') {
            const codec = h264BitstreamCodec(data);
            if (codec && codec !== remote.bitstreamCodec) {
                remote.bitstreamCodec = codec;
                remote.decoder.close();
                this._openDecoder(streamId, remote);
            }
        }
        // A decoder that has not seen a keyframe yet cannot use deltas; feeding them wastes work.
        if (!remote.primed && !isKeyframe) return false;
        if (remote.decoder.decodeQueueSize >= 6 || (discontinuity && !isKeyframe)) {
            this._recoverDecoder(streamId, remote);
            return false;
        }
        try {
            if (remote.pending.size >= 32) remote.pending.delete(remote.pending.keys().next().value);
            remote.pending.set(timestamp, performance.now());
            remote.decoder.decode(new EncodedVideoChunk(
                { type: isKeyframe ? 'key' : 'delta', timestamp, data }));
            remote.primed = true;
            return true;
        } catch { this._recoverDecoder(streamId, remote); return false; }
    }

    _recoverDecoder(streamId, remote) {
        if (remote.decoder?.state !== 'closed') remote.decoder?.close();
        this._openDecoder(streamId, remote);
        // Ignore dependent deltas until a new reference picture arrives.
        void this.hostRef?.invokeMethodAsync('OnVideoDecodeFailed', streamId);
    }

    _render(remote, frame) {
        try {
            const submitted = remote.pending.get(frame.timestamp);
            remote.pending.delete(frame.timestamp);
            if (submitted !== undefined) void this._considerDecoderLatency(remote, frame, performance.now() - submitted);
            if (remote.width !== frame.displayWidth || remote.height !== frame.displayHeight) {
                remote.width = remote.canvas.width = frame.displayWidth;
                remote.height = remote.canvas.height = frame.displayHeight;
            }
            remote.context.drawImage(frame, 0, 0);
        } catch { /* A detached canvas is a closed tile, not a call failure. */ }
        finally { frame.close(); }
    }

    async _considerDecoderLatency(remote, frame, elapsed) {
        // Some hardware decoders buffer several pictures despite optimizeForLatency. Try a
        // software decoder once, only after sustained measured delay at a bounded picture size.
        // Measure time inside the decoder, not network transit or the sender's unrelated clock.
        if (remote.codec !== 'h264') return;
        const tooLarge = frame.displayWidth * frame.displayHeight > 1920 * 1080;
        remote.lateFrames = elapsed > 100 ? remote.lateFrames + 1 : 0;
        if (remote.software && (tooLarge || remote.lateFrames >= 12)) {
            remote.software = false;
            this._recoverDecoder(remote.streamId, remote);
            return;
        }
        if (tooLarge || remote.fallbackTried || remote.lateFrames < 12) return;
        remote.fallbackTried = true;
        const decoder = remote.decoder;
        try {
            const support = await VideoDecoder.isConfigSupported({
                codec: remote.bitstreamCodec || videoCodecString('h264', 1080),
                codedWidth: frame.displayWidth, codedHeight: frame.displayHeight,
                hardwareAcceleration: 'prefer-software', optimizeForLatency: true
            });
            if (!support?.supported || this.remotes.get(remote.streamId) !== remote || remote.decoder !== decoder) return;
            remote.software = true;
            this._recoverDecoder(remote.streamId, remote);
        } catch { /* Unsupported software paths keep the native decoder. */ }
    }

    /// Releases the camera outright - track.stop() is what turns the hardware indicator off.
    stopCapture(reason) {
        const wasRunning = this.captureRunning;
        this.captureGeneration++;
        this.captureRunning = false;
        if (this.captureReader) { try { this.captureReader.cancel(); } catch { } this.captureReader = null; }
        this._stopFrameCallbacks();
        // The next camera has its own shape; leaving these set would fit it to the old one's aspect.
        this.sourceWidth = this.sourceHeight = 0;
        this.rotateCanvas = this.rotateContext = null;
        if (this.preview) this.preview.srcObject = null;
        if (this.mediaStream) { this.mediaStream.getTracks().forEach(t => t.stop()); this.mediaStream = null; }
        this.stats = { fps: 0, kbps: 0, dropped: this.stats.dropped, backlog: 0 };
        const notify = this.dotNetRef;
        this.dotNetRef = null;
        if (wasRunning && reason && notify) void notify.invokeMethodAsync('OnVideoCaptureStopped', reason);
        return wasRunning;
    }

    _closeEncoder() {
        if (this.encoder && this.encoder.state !== 'closed') { try { this.encoder.close(); } catch { } }
        this.encoder = null;
    }

    async dispose() {
        globalThis.document?.removeEventListener('visibilitychange', this.hidden);
        globalThis.removeEventListener?.('pagehide', this.hidden);
        this.stopCapture();
        this._closeEncoder();
        for (const streamId of [...this.remotes.keys()]) this.removeRemote(streamId);
        this.hostRef = null;
        this.preview = null;
        this.config = null;
    }
}

// ─── Device Manager ─────────────────────────────────

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

// ─── Exports ────────────────────────────────────────

export function createAudioPipeline() { return new AudioPipeline(); }
export function requireSecureVoiceContext() {
    if (!globalThis.isSecureContext)
        throw new Error('Voice calls require a secure HTTPS page.');
}
export async function checkVoiceCapabilities() {
    if (!globalThis.isSecureContext)
        return { supported: false, reason: 'Open Yap using its HTTPS address to use voice calls.' };
    if (typeof AudioWorkletNode === 'undefined' ||
        typeof AudioContext === 'undefined' || !navigator.mediaDevices?.getUserMedia)
        return { supported: false, reason: 'Voice calls are not supported by this browser. Try an updated browser.' };
    try {
        const config = { codec: 'opus', sampleRate: 48000, numberOfChannels: 1 };
        const [encoder, decoder] = await Promise.all([
            AudioEncoder.isConfigSupported({ ...config, bitrate: 128000 }),
            AudioDecoder.isConfigSupported(config)
        ]);
        if (typeof AudioData !== 'undefined' && encoder.supported && decoder.supported)
            return { supported: true, nativeCodecs: true, reason: null };
    } catch { /* Unsupported configurations must fail before an invitation is sent. */ }
    // Concentus in the .NET WASM runtime supplies Opus when Safari lacks WebCodecs audio.
    return { supported: true, nativeCodecs: false, reason: null };
}
export function createVideoPipeline() { return new VideoPipeline(); }

/// Everything the caller needs to decide whether, and how well, this device can send video.
/// Runs before the camera is touched: probing never opens a capture device.
export async function checkVideoCapabilities() {
    if (!globalThis.isSecureContext)
        return { supported: false, reason: 'Open Yap using its HTTPS address to use video calls.', ceiling: 0, codecs: [] };
    // "Cannot encode video" reads as a hardware limit. A browser missing the capture or codec API
    // is a different problem with a different fix, and the user cannot guess it from the old words.
    if (typeof VideoEncoder === 'undefined' || typeof VideoFrame === 'undefined' ||
        !navigator.mediaDevices?.getUserMedia || videoCaptureStrategy() === 'none')
        return { supported: false, ceiling: 0, codecs: [],
            reason: 'This browser cannot send video in calls. Try the latest Safari, Chrome, Edge or Firefox.' };
    const ceiling = await videoDeviceCeiling();
    const codecs = await probeVideoCodecs(ceiling);
    return codecs.some(x => x.encode)
        ? { supported: true, reason: null, ceiling, codecs }
        : { supported: false, reason: 'This device has no video encoder for calls.', ceiling, codecs };
}

export async function enumerateAudioInputs() { return await DeviceManager.enumerateAudioInputs(); }
export async function enumerateVideoInputs() { return await DeviceManager.enumerateVideoInputs(); }
export async function enumerateAudioOutputs() { return await DeviceManager.enumerateAudioOutputs(); }
export async function checkPermissions() { return await DeviceManager.checkPermissions(); }
export async function requestPermissions(audio, video) { return await DeviceManager.requestPermissions(audio, video); }
export function isWebCodecsSupported() { return DeviceManager.isWebCodecsSupported(); }
