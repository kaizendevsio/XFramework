using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Shared.Abstractions;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Domain.Shared.Contracts.Requests;
using StreamFlow.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Configurations;

namespace XFramework.Integration.Services;

/// <summary>
/// Manages a pool of SignalR HubConnections for dynamic connection scaling.
/// Auto-scales based on pending invocation count per connection.
/// </summary>
public sealed class ConnectionPool : IAsyncDisposable
{
    private readonly List<PooledConnection> _connections = [];
    private readonly Lock _scaleLock = new();
    private readonly Func<HubConnection> _connectionFactory;
    private readonly Func<HubConnection, Task> _onConnectionReady;
    private readonly StreamFlowConfiguration _config;
    private readonly ILogger _logger;
    private readonly Timer _scaleTimer;
    private int _roundRobinCounter;
    private volatile bool _disposed;

    public ConnectionPool(
        Func<HubConnection> connectionFactory,
        Func<HubConnection, Task> onConnectionReady,
        StreamFlowConfiguration config,
        ILogger logger)
    {
        _connectionFactory = connectionFactory;
        _onConnectionReady = onConnectionReady;
        _config = config;
        _logger = logger;

        // Check scaling every 500ms
        _scaleTimer = new Timer(ScaleCheck, null, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(500));
    }

    /// <summary>
    /// Initialize the pool with MinConnections connections.
    /// The first connection is provided externally (already built by SignalRService).
    /// </summary>
    public void AddPrimary(HubConnection primary)
    {
        var pooled = new PooledConnection(primary);
        lock (_scaleLock)
        {
            _connections.Add(pooled);
        }
    }

    /// <summary>
    /// Get the best connection for a new request using round-robin.
    /// </summary>
    public PooledConnection GetConnection()
    {
        List<PooledConnection> snapshot;
        lock (_scaleLock)
        {
            snapshot = [.. _connections];
        }

        if (snapshot.Count == 0)
            throw new InvalidOperationException("No connections available in the pool");

        if (snapshot.Count == 1)
            return snapshot[0];

        // Round-robin across connected connections
        var connected = snapshot.Where(c => c.Connection.State == HubConnectionState.Connected && c.IsRegistered).ToList();
        if (connected.Count == 0)
            return snapshot[0]; // Fallback to first (it handles reconnection)

        var idx = (uint)Interlocked.Increment(ref _roundRobinCounter) % connected.Count;
        return connected[(int)idx];
    }

    /// <summary>
    /// Get all connections (for broadcasting events, cleanup, etc.)
    /// </summary>
    public IReadOnlyList<PooledConnection> GetAll()
    {
        lock (_scaleLock)
        {
            return [.. _connections];
        }
    }

    /// <summary>
    /// Total pending invocations across all connections.
    /// </summary>
    public int TotalPending => _connections.Sum(c => c.PendingCount);

    private void ScaleCheck(object? state)
    {
        if (_disposed) return;

        try
        {
            List<PooledConnection> snapshot;
            lock (_scaleLock)
            {
                snapshot = [.. _connections];
            }

            if (snapshot.Count == 0) return;

            // Scale up: if any connection exceeds threshold
            var maxPending = snapshot.Max(c => c.PendingCount);
            if (maxPending >= _config.ScaleUpThreshold && snapshot.Count < _config.MaxConnections)
            {
                _ = ScaleUpAsync();
            }

            // Scale down: remove idle connections (keep MinConnections)
            if (snapshot.Count > _config.MinConnections)
            {
                var now = DateTime.UtcNow;
                var idleTimeout = TimeSpan.FromSeconds(_config.IdleTimeoutSeconds);

                for (int i = snapshot.Count - 1; i >= _config.MinConnections; i--)
                {
                    var conn = snapshot[i];
                    if (conn.PendingCount == 0 && now - conn.LastUsed > idleTimeout)
                    {
                        _ = ScaleDownAsync(conn);
                        break; // Remove one at a time
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection pool scale check failed");
        }
    }

    private async Task ScaleUpAsync()
    {
        lock (_scaleLock)
        {
            if (_connections.Count >= _config.MaxConnections) return;
        }

        try
        {
            _logger.LogInformation("Connection pool scaling up: {Current}/{Max} connections",
                _connections.Count, _config.MaxConnections);

            var newConnection = _connectionFactory();
            await newConnection.StartAsync();
            await _onConnectionReady(newConnection);

            var pooled = new PooledConnection(newConnection) { IsRegistered = true };

            lock (_scaleLock)
            {
                if (_connections.Count < _config.MaxConnections)
                {
                    _connections.Add(pooled);
                    _logger.LogInformation("Connection pool scaled up to {Count} connections", _connections.Count);
                }
                else
                {
                    // Another thread beat us
                    _ = newConnection.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scale up connection pool");
        }
    }

    private async Task ScaleDownAsync(PooledConnection conn)
    {
        lock (_scaleLock)
        {
            if (_connections.Count <= _config.MinConnections) return;
            _connections.Remove(conn);
        }

        try
        {
            _logger.LogInformation("Connection pool scaled down to {Count} connections", _connections.Count);
            await conn.Connection.StopAsync();
            await conn.Connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing scaled-down connection");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _scaleTimer.DisposeAsync();

        List<PooledConnection> snapshot;
        lock (_scaleLock)
        {
            snapshot = [.. _connections];
            _connections.Clear();
        }

        foreach (var conn in snapshot)
        {
            try
            {
                await conn.Connection.DisposeAsync();
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}

/// <summary>
/// A single connection in the pool with its own pending call tracking.
/// </summary>
public sealed class PooledConnection
{
    public HubConnection Connection { get; }
    public ConcurrentDictionary<Guid, PooledRpcCall> PendingCalls { get; } = new();
    public volatile bool IsRegistered;
    public DateTime LastUsed = DateTime.UtcNow;

    public int PendingCount => PendingCalls.Count;

    public PooledConnection(HubConnection connection)
    {
        Connection = connection;
    }

    public void Touch() => LastUsed = DateTime.UtcNow;
}
