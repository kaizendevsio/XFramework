/**
 * WebCodecs integration for hardware-accelerated audio/video encoding/decoding.
 * Uses the browser's native codecs (Opus, H.264, H.265) via the WebCodecs API.
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

  reconfigureBitrate(sampleRate: number, channels: number, newBitrate: number): void {
    if (this.encoder?.state === 'configured') {
      this.encoder.configure({
        codec: 'opus',
        sampleRate,
        numberOfChannels: channels,
        bitrate: newBitrate,
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

/** Video codec type for WebCodecs initialization. */
export type VideoCodecType = 'h264' | 'h265';

/** Codec strings for WebCodecs. */
const CODEC_STRINGS: Record<VideoCodecType, string> = {
  h264: 'avc1.42001f', // H.264 Baseline Level 3.1
  h265: 'hev1.1.6.L93.B0', // H.265 Main Profile Level 3.1
};

export class VideoCodecHelper {
  private encoder: VideoEncoder | null = null;
  private decoder: VideoDecoder | null = null;
  private keyframeInterval = 60;
  private frameCount = 0;
  private codecType: VideoCodecType = 'h264';
  private width = 1280;
  private height = 720;
  private framerate = 30;

  /** Fires when the encoder produces an encoded chunk. */
  public onEncodedChunk?: (data: Uint8Array, isKeyframe: boolean) => void;
  /** Fires when the decoder produces a decoded video frame. */
  public onDecodedFrame?: (frame: VideoFrame) => void;

  /**
   * Check if H.265 encoding is supported in this browser.
   * Not all browsers support HEVC via WebCodecs.
   */
  static async isH265Supported(): Promise<boolean> {
    try {
      const support = await VideoEncoder.isConfigSupported({
        codec: CODEC_STRINGS.h265,
        width: 1280,
        height: 720,
        bitrate: 2_000_000,
        framerate: 30,
      });
      return support.supported === true;
    } catch {
      return false;
    }
  }

  async initEncoder(
    width = 1280,
    height = 720,
    bitrate = 2_000_000,
    framerate = 30,
    codec: VideoCodecType = 'h264'
  ): Promise<void> {
    this.codecType = codec;
    this.width = width;
    this.height = height;
    this.framerate = framerate;

    this.encoder = new VideoEncoder({
      output: (chunk) => {
        const buf = new Uint8Array(chunk.byteLength);
        chunk.copyTo(buf);
        const isKeyframe = chunk.type === 'key';
        this.onEncodedChunk?.(buf, isKeyframe);
      },
      error: (e) => console.error('VideoEncoder error:', e),
    });

    this.encoder.configure({
      codec: CODEC_STRINGS[codec],
      width,
      height,
      bitrate,
      framerate,
      latencyMode: 'realtime',
    });
  }

  async initDecoder(codec: VideoCodecType = 'h264'): Promise<void> {
    this.codecType = codec;

    this.decoder = new VideoDecoder({
      output: (frame) => {
        this.onDecodedFrame?.(frame);
      },
      error: (e) => console.error('VideoDecoder error:', e),
    });

    this.decoder.configure({
      codec: CODEC_STRINGS[codec],
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
    this.frameCount = this.keyframeInterval - 1;
  }

  /** Reconfigure encoder with new bitrate. */
  reconfigureBitrate(newBitrate: number): void {
    if (this.encoder?.state === 'configured') {
      this.encoder.configure({
        codec: CODEC_STRINGS[this.codecType],
        width: this.width,
        height: this.height,
        bitrate: newBitrate,
        framerate: this.framerate,
        latencyMode: 'realtime',
      });
    }
  }

  /** Reconfigure encoder with new resolution and framerate. */
  reconfigureResolution(width: number, height: number, framerate?: number): void {
    if (this.encoder?.state === 'configured') {
      this.width = width;
      this.height = height;
      if (framerate) this.framerate = framerate;
      this.encoder.configure({
        codec: CODEC_STRINGS[this.codecType],
        width,
        height,
        bitrate: 2_000_000, // Will be overridden by ABR
        framerate: this.framerate,
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
