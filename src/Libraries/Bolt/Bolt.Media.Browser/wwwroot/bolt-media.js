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
        this._lastConfig = null;
    }

    async initEncoder(width, height, bitrate, framerate, codec, keyframeInterval) {
        this.keyframeInterval = keyframeInterval || 60;
        this.frameCount = 0;

        const codecString = codec === 'h265' ? 'hev1.1.6.L93.B0' : 'avc1.42001f';

        this._lastConfig = {
            codec: codecString,
            width: width,
            height: height,
            bitrate: bitrate * 1000,
            framerate: framerate,
            latencyMode: 'realtime',
            hardwareAcceleration: 'prefer-hardware'
        };

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
        this.encoder.configure(this._lastConfig);
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
        this.frameCount = this.keyframeInterval - 1;
    }

    reconfigureBitrate(newBitrate) {
        if (!this.encoder || this.encoder.state !== 'configured' || !this._lastConfig) return;
        this._lastConfig.bitrate = newBitrate * 1000;
        this.encoder.configure(this._lastConfig);
    }

    reconfigureResolution(width, height, framerate) {
        if (!this.encoder || this.encoder.state !== 'configured' || !this._lastConfig) return;
        this._lastConfig.width = width;
        this._lastConfig.height = height;
        if (framerate) this._lastConfig.framerate = framerate;
        this.encoder.configure(this._lastConfig);
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

export async function enumerateAudioInputs() { return await DeviceManager.enumerateAudioInputs(); }
export async function enumerateVideoInputs() { return await DeviceManager.enumerateVideoInputs(); }
export async function enumerateAudioOutputs() { return await DeviceManager.enumerateAudioOutputs(); }
export async function checkPermissions() { return await DeviceManager.checkPermissions(); }
export async function requestPermissions(audio, video) { return await DeviceManager.requestPermissions(audio, video); }
export function isWebCodecsSupported() { return DeviceManager.isWebCodecsSupported(); }
