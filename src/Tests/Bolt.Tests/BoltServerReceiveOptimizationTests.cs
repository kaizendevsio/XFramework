using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Bolt.Protocol;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(15000)]
public sealed class BoltServerReceiveOptimizationTests
{
    [Test]
    public async Task HandleConnectionAsync_FragmentedFrame_ReturnsAssemblyBufferBeforeDisconnect()
    {
        var pool = new TrackingArrayPool();
        using var server = CreateServer(pool, new BoltServerOptions
        {
            MaxFrameBytes = 1024 * 1024,
            ReceiveBufferBytes = 1024
        });
        await using var transport = new FragmentedConnection();
        var frame = WriteRegister();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);

        transport.Enqueue(frame[..5], endOfMessage: false);
        transport.Enqueue(frame[5..], endOfMessage: true);

        var assemblyBuffer = await pool.WaitForRentAsync(index: 1);
        await pool.WaitForReturnAsync(assemblyBuffer);

        serverTask.IsCompleted.Should().BeFalse("the assembly buffer should be returned while the connection is still active");
        pool.GetReturnCount(assemblyBuffer).Should().Be(1);

        transport.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
        pool.AssertEveryRentalReturnedExactlyOnce();
    }

    [Test]
    public async Task HandleConnectionAsync_CanceledFragmentAssembly_ReturnsEveryReceiveBufferExactlyOnce()
    {
        var pool = new TrackingArrayPool();
        using var server = CreateServer(pool, new BoltServerOptions
        {
            MaxFrameBytes = 1024 * 1024,
            ReceiveBufferBytes = 1024
        });
        await using var transport = new FragmentedConnection();
        using var cancellation = new CancellationTokenSource();
        var serverTask = server.HandleConnectionAsync(transport, cancellation.Token);

        transport.Enqueue(WriteRegister()[..5], endOfMessage: false);
        await pool.WaitForRentAsync(index: 1);
        cancellation.Cancel();

        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
        pool.AssertEveryRentalReturnedExactlyOnce();
    }

    [Test]
    public async Task HandleConnectionAsync_FragmentedMalformedFrame_ReturnsEveryReceiveBufferExactlyOnce()
    {
        var pool = new TrackingArrayPool();
        using var server = CreateServer(pool, new BoltServerOptions
        {
            MaxFrameBytes = 1024 * 1024,
            ReceiveBufferBytes = 1024
        });
        await using var transport = new FragmentedConnection();
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);

        transport.Enqueue([(byte)FrameType.Register], endOfMessage: false);
        transport.Enqueue([0], endOfMessage: true);

        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
        pool.AssertEveryRentalReturnedExactlyOnce();
    }

    [Test]
    public async Task HandleConnectionAsync_DisconnectDuringFragmentAssembly_ReturnsEveryReceiveBufferExactlyOnce()
    {
        var pool = new TrackingArrayPool();
        using var server = CreateServer(pool, new BoltServerOptions
        {
            MaxFrameBytes = 1024 * 1024,
            ReceiveBufferBytes = 1024
        });
        await using var transport = new FragmentedConnection();
        var registrations = 0;
        server.ClientRegistered += (_, _) =>
        {
            Interlocked.Increment(ref registrations);
            return Task.CompletedTask;
        };
        var serverTask = server.HandleConnectionAsync(transport, CancellationToken.None);

        transport.Enqueue(WriteRegister(), endOfMessage: false);
        await pool.WaitForRentAsync(index: 1);
        transport.Complete();

        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));
        registrations.Should().Be(0, "a socket close cannot complete a fragmented message");
        pool.AssertEveryRentalReturnedExactlyOnce();
    }

    [Test]
    public async Task HandleConnectionAsync_ValidFragmentedDispatchThrows_ReturnsEveryReceiveBufferExactlyOnce()
    {
        var pool = new TrackingArrayPool();
        using var cancellation = new CancellationTokenSource();
        var authorizer = new CancelingTopicAuthorizer(cancellation);
        using var server = CreateServer(
            pool,
            new BoltServerOptions
            {
                MaxFrameBytes = 1024 * 1024,
                ReceiveBufferBytes = 1024
            },
            [authorizer]);
        await using var transport = new FragmentedConnection();
        var serverTask = server.HandleConnectionAsync(transport, cancellation.Token);
        transport.Enqueue(WriteRegister(), endOfMessage: true);
        var subscribe = WriteSubscribe();

        transport.Enqueue(subscribe[..7], endOfMessage: false);
        transport.Enqueue(subscribe[7..], endOfMessage: true);

        await authorizer.Invoked.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await serverTask.WaitAsync(TimeSpan.FromSeconds(3));

        authorizer.InvocationCount.Should().Be(1);
        pool.RentalCount.Should().Be(2, "the connection should rent one base and one assembly buffer");
        pool.AssertEveryRentalReturnedExactlyOnce();
    }

    [TestCase(8 * 1024 * 1024, 256 * 1024, 256 * 1024)]
    [TestCase(8 * 1024 * 1024, 0, 1024)]
    [TestCase(4096, 8192, 4096)]
    [TestCase(512, 8192, 1024)]
    public async Task HandleConnectionAsync_ReceiveBufferOption_UsesDefaultAndClampedSize(
        int maxFrameBytes,
        int configuredReceiveBufferBytes,
        int expectedReceiveBufferBytes)
    {
        var options = new BoltServerOptions { MaxFrameBytes = maxFrameBytes };
        if (configuredReceiveBufferBytes != 256 * 1024)
            options.ReceiveBufferBytes = configuredReceiveBufferBytes;

        var pool = new TrackingArrayPool();
        using var server = CreateServer(pool, options);
        await using var transport = new ClosedConnection();

        await server.HandleConnectionAsync(transport, CancellationToken.None);

        pool.RentRequests.Should().ContainSingle().Which.Should().Be(expectedReceiveBufferBytes);
        pool.AssertEveryRentalReturnedExactlyOnce();
    }

    private static BoltServer CreateServer(
        TrackingArrayPool pool,
        BoltServerOptions options,
        IEnumerable<IBoltTopicAuthorizer>? topicAuthorizers = null) =>
        new(NullLogger<BoltServer>.Instance, options, topicAuthorizers, pool);

    private static byte[] WriteRegister()
    {
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteRegister(writer, "receive-owner", "ReceiveOwner");
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] WriteSubscribe()
    {
        var writer = new ArrayBufferWriter<byte>();
        BoltCodec.WriteSubscribe(writer, "receive.throw", "receive-owner", durable: false);
        return writer.WrittenSpan.ToArray();
    }

    private sealed class CancelingTopicAuthorizer(CancellationTokenSource cancellation) : IBoltTopicAuthorizer
    {
        private int _invocationCount;

        public TaskCompletionSource Invoked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int InvocationCount => Volatile.Read(ref _invocationCount);

        public ValueTask<bool> AuthorizeAsync(
            BoltTopicAuthorizationContext context,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref _invocationCount);
            Invoked.TrySetResult();
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
            throw new AssertionException("The linked connection token was not canceled.");
        }
    }

    private sealed class TrackingArrayPool : ArrayPool<byte>
    {
        private readonly object _sync = new();
        private readonly List<byte[]> _rentals = [];
        private readonly Dictionary<byte[], int> _returns = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<byte[], TaskCompletionSource> _returnSignals = new(ReferenceEqualityComparer.Instance);
        private readonly TaskCompletionSource _rented = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<int> RentRequests { get; } = [];

        public int RentalCount
        {
            get
            {
                lock (_sync)
                    return _rentals.Count;
            }
        }

        public override byte[] Rent(int minimumLength)
        {
            var buffer = new byte[minimumLength];
            lock (_sync)
            {
                RentRequests.Add(minimumLength);
                _rentals.Add(buffer);
                _returns.Add(buffer, 0);
                _returnSignals.Add(buffer, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
                _rented.TrySetResult();
            }

            return buffer;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            TaskCompletionSource signal;
            lock (_sync)
            {
                _returns[array]++;
                signal = _returnSignals[array];
            }

            signal.TrySetResult();
        }

        public async Task<byte[]> WaitForRentAsync(int index)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_rentals.Count > index)
                        return _rentals[index];
                }

                await _rented.Task.WaitAsync(TimeSpan.FromSeconds(3));
                lock (_sync)
                {
                    if (_rentals.Count <= index)
                        _rented.TrySetResult();
                }
                await Task.Yield();
            }
        }

        public Task WaitForReturnAsync(byte[] buffer)
        {
            lock (_sync)
                return _returnSignals[buffer].Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        public int GetReturnCount(byte[] buffer)
        {
            lock (_sync)
                return _returns[buffer];
        }

        public void AssertEveryRentalReturnedExactlyOnce()
        {
            lock (_sync)
                _returns.Values.Should().OnlyContain(count => count == 1);
        }
    }

    private sealed class FragmentedConnection : IBoltConnection
    {
        private readonly Channel<(byte[] Data, bool EndOfMessage)> _incoming = Channel.CreateUnbounded<(byte[], bool)>();
        private int _isConnected = 1;

        public bool SupportsDatagrams => false;
        public bool IsConnected => Volatile.Read(ref _isConnected) != 0;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public void Enqueue(byte[] data, bool endOfMessage) =>
            _incoming.Writer.TryWrite((data, endOfMessage)).Should().BeTrue();

        public void Complete() => _incoming.Writer.TryComplete();

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default)
        {
            try
            {
                var item = await _incoming.Reader.ReadAsync(ct);
                item.Data.CopyTo(buffer);
                return (item.Data.Length, item.EndOfMessage);
            }
            catch (ChannelClosedException)
            {
                Interlocked.Exchange(ref _isConnected, 0);
                return (0, true);
            }
        }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default)
        {
            Interlocked.Exchange(ref _isConnected, 0);
            Complete();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _isConnected, 0);
            Complete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ClosedConnection : IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected => true;
        public BoltTransport TransportType => BoltTransport.WebSocket;
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            ValueTask.FromResult((0, true));
        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
