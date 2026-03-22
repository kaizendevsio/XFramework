using System.Collections.Concurrent;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Stream.Interfaces;

namespace StreamFlow.Stream.Services;

/// <summary>
/// Implementation of caching service for StreamFlow.
/// Uses ConcurrentDictionary for client tracking and StreamFlowMessageQueue for message queueing.
/// </summary>
public sealed class CachingService : ICachingService, IDisposable
{
    private readonly Timer _evictionTimer;
    private const int MaxAbsoluteClients = 10_000;
    private static readonly TimeSpan EvictionTtl = TimeSpan.FromHours(24);

    public CachingService(StreamFlowMessageQueue messageQueue)
    {
        MessageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
        _evictionTimer = new Timer(EvictStaleClients, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public ConcurrentDictionary<int, StreamFlowClient> Clients { get; set; } = new();
    public ConcurrentDictionary<int, StreamFlowClient> AbsoluteClients { get; set; } = new();
    public ConcurrentDictionary<string, ConcurrentBag<int>> ClientsByServiceId { get; } = new();
    public ConcurrentDictionary<string, int> ClientKeyByStreamId { get; } = new();
    public ConcurrentDictionary<string, int> AbsoluteClientKeyByServiceId { get; } = new();

    public StreamFlowMessageQueue MessageQueue { get; }

    private void EvictStaleClients(object? state)
    {
        var cutoff = DateTime.UtcNow - EvictionTtl;
        var toRemove = AbsoluteClients
            .Where(kvp => kvp.Value.LastSeenAt < cutoff && kvp.Value.LastSeenAt != default)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            if (AbsoluteClients.TryRemove(key, out var removed))
                AbsoluteClientKeyByServiceId.TryRemove(removed.Id, out _);
        }

        // Cap at max if still over
        if (AbsoluteClients.Count > MaxAbsoluteClients)
        {
            var excess = AbsoluteClients
                .OrderBy(kvp => kvp.Value.LastSeenAt)
                .Take(AbsoluteClients.Count - MaxAbsoluteClients)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in excess)
            {
                if (AbsoluteClients.TryRemove(key, out var removed))
                    AbsoluteClientKeyByServiceId.TryRemove(removed.Id, out _);
            }
        }
    }

    public void Dispose()
    {
        _evictionTimer.Dispose();
    }
}
