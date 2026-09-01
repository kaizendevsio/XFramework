using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace XFramework.Portal.Services;

public sealed class PortalActorTokenRefreshCoordinator
{
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<Guid, RefreshState> _states = new();

    public async Task<PortalActorTokenPair?> RefreshAsync(
        Guid sessionId,
        string refreshToken,
        Func<CancellationToken, Task<PortalActorTokenPair?>> refresh,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        ArgumentNullException.ThrowIfNull(refresh);

        RemoveExpiredStates();
        var state = _states.GetOrAdd(sessionId, static _ => new RefreshState());
        await state.Gate.WaitAsync(ct);
        try
        {
            state.LastAccessedUtc = DateTime.UtcNow;
            var tokenDigest = ComputeDigest(refreshToken);
            if (state.CurrentTokens is not null &&
                DigestsMatch(tokenDigest, state.PreviousRefreshTokenDigest))
            {
                return state.CurrentTokens;
            }

            if (state.CurrentTokens is not null &&
                !DigestsMatch(tokenDigest, state.CurrentRefreshTokenDigest))
            {
                return null;
            }

            var refreshed = await refresh(ct);
            if (refreshed is null || refreshed.SessionId != sessionId)
                return null;

            state.PreviousRefreshTokenDigest = tokenDigest;
            state.CurrentRefreshTokenDigest = ComputeDigest(refreshed.RefreshToken);
            state.CurrentTokens = refreshed;
            return refreshed;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public void Remove(Guid sessionId) => _states.TryRemove(sessionId, out _);

    private void RemoveExpiredStates()
    {
        var cutoff = DateTime.UtcNow - EntryLifetime;
        foreach (var entry in _states)
        {
            if (entry.Value.LastAccessedUtc < cutoff &&
                entry.Value.Gate.CurrentCount > 0)
            {
                _states.TryRemove(entry.Key, out _);
            }
        }
    }

    private static byte[] ComputeDigest(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static bool DigestsMatch(byte[] candidate, byte[]? expected) =>
        expected is not null && CryptographicOperations.FixedTimeEquals(candidate, expected);

    private sealed class RefreshState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public DateTime LastAccessedUtc { get; set; } = DateTime.UtcNow;
        public byte[]? PreviousRefreshTokenDigest { get; set; }
        public byte[]? CurrentRefreshTokenDigest { get; set; }
        public PortalActorTokenPair? CurrentTokens { get; set; }
    }
}

public sealed record PortalActorTokenPair(
    string AccessToken,
    string RefreshToken,
    Guid SessionId,
    int ExpiresIn);
