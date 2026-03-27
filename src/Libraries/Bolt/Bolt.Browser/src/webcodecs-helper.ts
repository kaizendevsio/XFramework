/**
 * WebCodecs integration for hardware-accelerated audio/video encoding/decoding.
 * Uses the browser's native codecs (Opus, H.264) via the WebCodecs API.
 */

export class AudioCodecHelper {
  private encoder: AudioEncoder | null = null;
  private decoder: AudioDecoder | null = null;

  /** Fires when the encoder produces an encoded chunk. */
  public onEncodedChunk?: (data: Uint8Array) => void;
  /** Fires when the decoder produces decoded audio data. */
  public onDecodedData?: (data: AudioData) => void;

  async initEncoder(sampleRate = 48000, channels = 1, bitrate = 64000): Promise<void> {
    this.encoder = new AudioEncoder({
      output: (chunk) => {
        const buf = new Uint8Array(chunk.byteLength);
        chunk.copyTo(buf);
        this.onEncodedChunk?.(buf);
      },
      error: (e) => console.error('AudioEncoder error:', e),
    });

    this.encoder.configure({
      codec: 'opus',
      sampleRate,
      numberOfChannels: channels,
      bitrate,
    });
  }

  async initDecoder(sampleRate = 48000, channels = 1): Promise<void> {
    this.decoder = new AudioDecoder({
      output: (data) => {
        this.onDecodedData?.(data);
      },
      error: (e) => console.error('AudioDecoder error:', e),
    });

    this.decoder.configure({
      codec: 'opus',
      sampleRate,
      numberOfChannels: channels,
    });
  }

  encode(audioData: AudioData): void {
    this.encoder?.encode(audioData);
  }

  decode(encodedData: Uint8Array, timestamp: number): void {
    const chunk = new EncodedAudioChunk({
      type: 'key',
      timestamp,
      data: encodedData,
    });
    this.decoder?.decode(chunk);
  }

  reconfigureBitrate(newBitrate: number): void {
    // AudioEncoder reconfigure requires full config
    if (this.encoder?.state === 'configured') {
      // Note: Opus encoder may not support dynamic reconfigure in all browsers
      // Fallback: close and re-create encoder
      console.log(`AudioCodec: bitrate change requested to ${newBitrate}bps`);
    }
  }

  close(): void {
    if (this.encoder?.state !== 'closed') this.encoder?.close();
    if (this.decoder?.state !== 'closed') this.decoder?.close();
    this.encoder = null;
    this.decoder = null;
  }
}

export class VideoCodecHelper {
  private encoder: VideoEncoder | null = null;
  private decoder: VideoDecoder | null = null;
  private keyframeInterval = 60; // Request keyframe every N frames
  private frameCount = 0;

  /** Fires when the encoder produces an encoded chunk. */
  public onEncodedChunk?: (data: Uint8Array, isKeyframe: boolean) => void;
  /** Fires when the decoder produces a decoded video frame. */
  public onDecodedFrame?: (frame: VideoFrame) => void;

  async initEncoder(width = 1280, height = 720, bitrate = 2_000_000, framerate = 30): Promise<void> {
    this.encoder = new VideoEncoder({
      output: (chunk, metadata) => {
        const buf = new Uint8Array(chunk.byteLength);
        chunk.copyTo(buf);
        const isKeyframe = chunk.type === 'key';
        this.onEncodedChunk?.(buf, isKeyframe);
      },
      error: (e) => console.error('VideoEncoder error:', e),
    });

    this.encoder.configure({
      codec: 'avc1.42001f', // H.264 Baseline Level 3.1
      width,
      height,
      bitrate,
      framerate,
      latencyMode: 'realtime',
    });
  }

  async initDecoder(): Promise<void> {
    this.decoder = new VideoDecoder({
      output: (frame) => {
        this.onDecodedFrame?.(frame);
      },
      error: (e) => console.error('VideoDecoder error:', e),
    });

    this.decoder.configure({
      codec: 'avc1.42001f',
    });
  }

  encode(frame: VideoFrame): void {
    if (!this.encoder) return;
    this.frameCount++;
    const keyFrame = this.frameCount % this.keyframeInterval === 0;
    this.encoder.encode(frame, { keyFrame });
    frame.close();
  }

  decode(encodedData: Uint8Array, timestamp: number, isKeyframe: boolean): void {
    const chunk = new EncodedVideoChunk({
      type: isKeyframe ? 'key' : 'delta',
      timestamp,
      data: encodedData,
    });
    this.decoder?.decode(chunk);
  }

  /** Force the next frame to be a keyframe. */
  requestKeyframe(): void {
    this.frameCount = this.keyframeInterval - 1; // Next encode will be keyframe
  }

  /** Reconfigure encoder with new bitrate. */
  reconfigureBitrate(newBitrate: number): void {
    if (this.encoder?.state === 'configured') {
      // VideoEncoder supports reconfigure for bitrate changes
      this.encoder.configure({
        codec: 'avc1.42001f',
        width: 1280,
        height: 720,
        bitrate: newBitrate,
        framerate: 30,
        latencyMode: 'realtime',
      });
    }
  }

  close(): void {
    if (this.encoder?.state !== 'closed') this.encoder?.close();
    if (this.decoder?.state !== 'closed') this.decoder?.close();
    this.encoder = null;
    this.decoder = null;
  }
}
