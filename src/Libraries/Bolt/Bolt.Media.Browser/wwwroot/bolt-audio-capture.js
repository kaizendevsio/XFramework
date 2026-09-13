// Twenty millisecond mono PCM frames. Limit cross-thread messages when the UI stalls.
class BoltAudioCapture extends AudioWorkletProcessor {
    constructor() {
        super();
        this.samples = new Float32Array(960);
        this.offset = 0;
        this.pending = 0;
        this.frames = 0;
        this.port.onmessage = () => { this.pending = Math.max(0, this.pending - 1); };
    }

    process(inputs) {
        const channels = inputs[0];
        if (!channels?.length) return true;
        for (let i = 0; i < channels[0].length; i++) {
            let sample = 0;
            for (const channel of channels) sample += channel[i];
            this.samples[this.offset++] = sample / channels.length;
            if (this.offset === 960) {
                if (this.pending < 2) {
                    this.port.postMessage({ samples: this.samples, timestamp: this.frames * 20000 }, [this.samples.buffer]);
                    this.pending++;
                    this.samples = new Float32Array(960);
                }
                this.offset = 0;
                this.frames++;
            }
        }
        return true;
    }
}

registerProcessor('bolt-audio-capture', BoltAudioCapture);
