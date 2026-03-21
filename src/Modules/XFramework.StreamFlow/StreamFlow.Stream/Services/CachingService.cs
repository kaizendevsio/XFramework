using System.Collections.Concurrent;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Stream.Interfaces;

namespace StreamFlow.Stream.Services;

/// <summary>
/// Implementation of caching service for StreamFlow.
/// Uses ConcurrentDictionary for client tracking and StreamFlowMessageQueue for message queueing.
/// </summary>
public sealed class CachingService : ICachingService
{
    public CachingService(StreamFlowMessageQueue messageQueue)
    {
        MessageQueue = messageQueue ?? throw new ArgumentNullException(nameof(messageQueue));
    }

    public ConcurrentDictionary<int, StreamFlowClient> Clients { get; set; } = new();
    public ConcurrentDictionary<int, StreamFlowClient> LatestClients { get; set; } = new();
    public ConcurrentDictionary<int, StreamFlowClient> AbsoluteClients { get; set; } = new();
    
    /// <summary>
    /// Message queue using bounded channels for high-performance message queueing.
    /// </summary>
    public StreamFlowMessageQueue MessageQueue { get; }
}