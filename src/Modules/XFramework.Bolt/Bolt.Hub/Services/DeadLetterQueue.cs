using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Bolt.Domain.Shared.BusinessObjects;
using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Configurations;

namespace Bolt.Hub.Services;

/// <summary>
/// Bounded dead letter queue for messages that cannot be delivered.
/// All silent message drop paths route here instead of being lost.
/// Uses DropOldest so the newest failures are always visible.
/// Capacity configurable via BoltConfiguration.DeadLetterQueueCapacity (default 100k).
/// </summary>
public sealed class DeadLetterQueue
{
    private readonly Channel<DeadLetterMessage> _channel;
    private readonly ILogger<DeadLetterQueue> _logger;
    private long _totalCount;

    public long TotalCount => Volatile.Read(ref _totalCount);
    public ChannelReader<DeadLetterMessage> Reader => _channel.Reader;

    public DeadLetterQueue(BoltConfiguration configuration, ILogger<DeadLetterQueue> logger)
    {
        _logger = logger;
        var capacity = configuration.DeadLetterQueueCapacity > 0
            ? configuration.DeadLetterQueueCapacity
            : 100_000;

        _channel = Channel.CreateBounded<DeadLetterMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public void Enqueue(BoltMessage message, string reason, int retryCount = 0, string? error = null)
    {
        var dlm = new DeadLetterMessage
        {
            RequestId = message.RequestId,
            CommandName = message.CommandName,
            RecipientId = message.RecipientId,
            SenderId = message.ClientId,
            Data = message.Data,
            DropReason = reason,
            RetryCount = retryCount,
            DroppedAt = DateTime.UtcNow,
            ErrorMessage = error
        };

        if (!_channel.Writer.TryWrite(dlm))
        {
            _logger.LogError("DLQ channel full (DropOldest), oldest entry evicted. RequestId: {RequestId}, Reason: {Reason}",
                message.RequestId, reason);
        }

        Interlocked.Increment(ref _totalCount);
        _logger.LogWarning("Dead letter: {Reason} — RequestId: {RequestId}, Recipient: {RecipientId}, Command: {CommandName}",
            reason, message.RequestId, message.RecipientId, message.CommandName);
    }
}
