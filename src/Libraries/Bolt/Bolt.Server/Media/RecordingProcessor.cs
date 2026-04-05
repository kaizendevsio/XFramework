using System.Collections.Concurrent;
using Bolt.Protocol;

namespace Bolt.Server.Media;

/// <summary>
/// Server-side media recording processor. Captures raw encoded media frames
/// to disk as binary container files. Each call produces:
/// - One .opus file per audio stream (raw Opus frames with length prefixes)
/// - One .h264 file per video stream (Annex-B style: length-prefixed NALUs)
///
/// Files are written to: {OutputDirectory}/{callId}/{streamId}.{ext}
///
/// This processor writes raw encoded frames, not muxed containers.
/// Post-processing can mux into Ogg/WebM/MP4 using FFmpeg:
///   ffmpeg -f s16le -ar 48000 -ac 1 -i audio.opus -c:a copy output.ogg
///
/// Thread safety: ProcessFrameAsync is called from the media tap channel's
/// single reader thread. File I/O is async-safe.
/// </summary>
public sealed class RecordingProcessor : IMediaProcessor, IAsyncDisposable
{
    private readonly string _outputDirectory;
    private readonly ConcurrentDictionary<Guid, CallRecording> _activeRecordings = new();
    private readonly HashSet<MediaType> _acceptedTypes;

    /// <summary>
    /// Create a recording processor.
    /// </summary>
    /// <param name="outputDirectory">Base directory for recording files.</param>
    /// <param name="recordAudio">Whether to record audio tracks.</param>
    /// <param name="recordVideo">Whether to record video tracks.</param>
    public RecordingProcessor(string outputDirectory, bool recordAudio = true, bool recordVideo = true)
    {
        _outputDirectory = outputDirectory;
        _acceptedTypes = [];
        if (recordAudio) _acceptedTypes.Add(MediaType.Audio);
        if (recordVideo) _acceptedTypes.Add(MediaType.Video);
    }

    public bool Accepts(Guid callId, MediaType mediaType) => _acceptedTypes.Contains(mediaType);

    public ValueTask OnCallStartedAsync(Guid callId)
    {
        var callDir = Path.Combine(_outputDirectory, callId.ToString("N"));
        Directory.CreateDirectory(callDir);
        _activeRecordings[callId] = new CallRecording(callDir);
        return ValueTask.CompletedTask;
    }

    public async ValueTask ProcessFrameAsync(Guid callId, Guid streamId, ReadOnlyMemory<byte> frameData, uint timestamp, uint sequenceNumber)
    {
        if (!_activeRecordings.TryGetValue(callId, out var recording))
            return;

        var stream = recording.GetOrCreateStream(streamId);
        await stream.WriteFrameAsync(frameData, timestamp, sequenceNumber);
    }

    public async ValueTask OnCallEndedAsync(Guid callId)
    {
        if (_activeRecordings.TryRemove(callId, out var recording))
            await recording.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, recording) in _activeRecordings)
            await recording.DisposeAsync();
        _activeRecordings.Clear();
    }
}

/// <summary>Tracks all streams being recorded for a single call.</summary>
internal sealed class CallRecording : IAsyncDisposable
{
    private readonly string _callDirectory;
    private readonly ConcurrentDictionary<Guid, StreamRecording> _streams = new();

    public CallRecording(string callDirectory) => _callDirectory = callDirectory;

    public StreamRecording GetOrCreateStream(Guid streamId)
    {
        return _streams.GetOrAdd(streamId, id =>
        {
            var filePath = Path.Combine(_callDirectory, $"{id:N}.bin");
            return new StreamRecording(filePath);
        });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, stream) in _streams)
            await stream.DisposeAsync();
        _streams.Clear();
    }
}

/// <summary>
/// Records a single media stream to a binary file.
/// Format: repeating [4:frameLength][4:timestamp][4:sequenceNumber][frameData]
/// </summary>
internal sealed class StreamRecording : IAsyncDisposable
{
    private FileStream? _file;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private long _totalBytes;

    public StreamRecording(string filePath)
    {
        _file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read,
            bufferSize: 65536, useAsync: true);
    }

    public async ValueTask WriteFrameAsync(ReadOnlyMemory<byte> frameData, uint timestamp, uint sequenceNumber)
    {
        if (_file == null) return;

        // Header: [4:length][4:timestamp][4:seq] = 12 bytes, little-endian
        var header = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0), frameData.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), timestamp);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), sequenceNumber);

        await _lock.WaitAsync();
        try
        {
            await _file.WriteAsync(header);
            await _file.WriteAsync(frameData);
            _totalBytes += 12 + frameData.Length;

            // Flush periodically (every ~1MB)
            if (_totalBytes % (1024 * 1024) < 65536)
                await _file.FlushAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_file != null)
        {
            await _file.FlushAsync();
            await _file.DisposeAsync();
            _file = null;
        }
        _lock.Dispose();
    }
}
