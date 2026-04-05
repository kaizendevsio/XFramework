using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Wallets.Api.Events;

/// <summary>
/// In-memory wallet event publisher that logs events, buffers them in a bounded queue,
/// and flags large transactions as warnings.
/// </summary>
public sealed class WalletEventPublisher : IWalletEventPublisher
{
    private const int MaxBufferedEvents = 1000;
    private const decimal DefaultLargeTransactionThreshold = 10_000m;

    private readonly ILogger<WalletEventPublisher> _logger;
    private readonly Channel<WalletEvent> _channel;
    private readonly ConcurrentQueue<WalletEvent> _recentEvents = new();
    private readonly decimal _largeTransactionThreshold;

    public WalletEventPublisher(ILogger<WalletEventPublisher> logger, IConfiguration configuration)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<WalletEvent>(new BoundedChannelOptions(MaxBufferedEvents)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _largeTransactionThreshold = configuration.GetValue<decimal?>(
            "WalletEvents:LargeTransactionThreshold") ?? DefaultLargeTransactionThreshold;
    }

    /// <inheritdoc />
    public async Task PublishAsync(WalletEvent walletEvent)
    {
        // Structured logging of the event
        _logger.LogInformation(
            "Wallet event published: {EventType} | EventId={EventId} WalletId={WalletId} CredentialId={CredentialId} TenantId={TenantId}",
            walletEvent.EventType,
            walletEvent.EventId,
            walletEvent.WalletId,
            walletEvent.CredentialId,
            walletEvent.TenantId);

        // Check for large transaction
        CheckLargeTransaction(walletEvent);

        // Store in bounded queue for query access
        _recentEvents.Enqueue(walletEvent);
        while (_recentEvents.Count > MaxBufferedEvents)
        {
            _recentEvents.TryDequeue(out _);
        }

        // Write to channel for future webhook delivery
        await _channel.Writer.WriteAsync(walletEvent);
    }

    /// <inheritdoc />
    public IReadOnlyList<WalletEvent> GetRecentEvents(
        Guid? walletId = null,
        Guid? credentialId = null,
        string? eventType = null,
        int pageIndex = 0,
        int pageSize = 50)
    {
        var query = _recentEvents.AsEnumerable();

        if (walletId.HasValue && walletId.Value != Guid.Empty)
            query = query.Where(e => e.WalletId == walletId.Value);

        if (credentialId.HasValue && credentialId.Value != Guid.Empty)
            query = query.Where(e => e.CredentialId == credentialId.Value);

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));

        return query
            .OrderByDescending(e => e.OccurredAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();
    }

    private void CheckLargeTransaction(WalletEvent walletEvent)
    {
        decimal? amount = walletEvent switch
        {
            TransactionCompletedEvent tc => Math.Abs(tc.Amount),
            LargeTransactionEvent lt => Math.Abs(lt.Amount),
            TransactionReversedEvent tr => Math.Abs(tr.Amount),
            _ => null
        };

        if (amount.HasValue && amount.Value >= _largeTransactionThreshold)
        {
            _logger.LogWarning(
                "Large transaction detected: {EventType} | Amount={Amount} Threshold={Threshold} WalletId={WalletId} CredentialId={CredentialId}",
                walletEvent.EventType,
                amount.Value,
                _largeTransactionThreshold,
                walletEvent.WalletId,
                walletEvent.CredentialId);
        }
    }
}
