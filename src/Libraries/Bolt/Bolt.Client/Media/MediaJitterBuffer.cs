using System.Threading.Channels;

namespace Bolt.Client.Media;

public readonly record struct BufferedFrame(uint SequenceNumber, uint Timestamp, ReadOnlyMemory<byte> Data, bool IsKeyframe);

public sealed class MediaJitterBuffer : IAsyncDisposable
{
    private readonly Channel<BufferedFrame> _output;
    private readonly SortedList<uint, BufferedFrame> _buffer = new();
    private readonly object _lock = new();
    private readonly int _maxBufferSize;
    private readonly int _frameIntervalMs;
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private int _targetDelayMs;
    private double _jitterEma;
    private long _lastArrivalTicks;
    private uint _nextExpectedSeq = uint.MaxValue;
    private bool _started;
    private readonly PeriodicTimer _playbackTimer;
    private readonly CancellationTokenSource _cts = new();
    private Task? _playbackTask;

    public int TargetDelayMs => _targetDelayMs;
    public double MeasuredJitterMs => _jitterEma;

    public MediaJitterBuffer(bool isAudio)
    {
        _frameIntervalMs = isAudio ? 20 : 33;
        _minDelayMs = isAudio ? 20 : 40;
        _maxDelayMs = isAudio ? 200 : 300;
        _targetDelayMs = isAudio ? 50 : 80;
        _maxBufferSize = 10;
        _output = Channel.CreateBounded<BufferedFrame>(new BoundedChannelOptions(_maxBufferSize * 2)
            { FullMode = BoundedChannelFullMode.DropOldest });
        _playbackTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_frameIntervalMs));
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        _playbackTask = PlaybackLoopAsync(_cts.Token);
    }

    public void Enqueue(uint sequenceNumber, uint timestamp, ReadOnlyMemory<byte> data, bool isKeyframe)
    {
        var nowTicks = Environment.TickCount64;
        lock (_lock)
        {
            if (_lastArrivalTicks > 0)
            {
                var interArrivalMs = (double)(nowTicks - _lastArrivalTicks);
                var deviation = Math.Abs(interArrivalMs - _frameIntervalMs);
                _jitterEma = _jitterEma * 0.95 + deviation * 0.05;
                _targetDelayMs = Math.Clamp((int)(2 * _jitterEma + 10), _minDelayMs, _maxDelayMs);
            }
            _lastArrivalTicks = nowTicks;

            if (_buffer.Count >= _maxBufferSize)
                _buffer.RemoveAt(0);

            _buffer[sequenceNumber] = new BufferedFrame(sequenceNumber, timestamp, data, isKeyframe);
        }
    }

    public IAsyncEnumerable<BufferedFrame> ReadAllAsync(CancellationToken ct = default)
        => _output.Reader.ReadAllAsync(ct);

    private async Task PlaybackLoopAsync(CancellationToken ct)
    {
        await Task.Delay(_targetDelayMs, ct);

        while (await _playbackTimer.WaitForNextTickAsync(ct))
        {
            BufferedFrame? frame = null;
            lock (_lock)
            {
                if (_buffer.Count > 0)
                {
                    var first = _buffer.GetValueAtIndex(0);
                    if (_nextExpectedSeq == uint.MaxValue) _nextExpectedSeq = first.SequenceNumber;

                    if (_buffer.TryGetValue(_nextExpectedSeq, out var expected))
                    {
                        frame = expected;
                        _buffer.Remove(_nextExpectedSeq);
                        _nextExpectedSeq++;
                    }
                    else
                    {
                        frame = first;
                        _buffer.RemoveAt(0);
                        _nextExpectedSeq = first.SequenceNumber + 1;
                    }
                }
            }
            if (frame.HasValue)
                await _output.Writer.WriteAsync(frame.Value, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _playbackTimer.Dispose();
        _output.Writer.TryComplete();
        if (_playbackTask != null)
            try { await _playbackTask; } catch (OperationCanceledException) { }
        _cts.Dispose();
    }
}
