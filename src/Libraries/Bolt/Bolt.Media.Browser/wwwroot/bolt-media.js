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

// ─── Video Pipeline ──────────────────────────────────

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

// ─── Exports ────────────────────────────────────────

export function createAudioPipeline() { return new AudioPipeline(); }
export function createVideoPipeline() { return new VideoPipeline(); }

// Device manager exports (placeholder — Task 5)
export async function enumerateAudioInputs() { return []; }
export async function enumerateVideoInputs() { return []; }
export async function enumerateAudioOutputs() { return []; }
export async function checkPermissions() { return { audio: 'prompt', video: 'prompt' }; }
export async function requestPermissions(audio, video) { return false; }
export function isWebCodecsSupported() { return typeof AudioEncoder !== 'undefined' && typeof VideoEncoder !== 'undefined'; }
