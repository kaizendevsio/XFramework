using System.Collections.Concurrent;

namespace Bolt.Hub.Services;

public sealed class BoltServicePresenceTracker : IBoltServicePresenceTracker
{
    private readonly ConcurrentDictionary<string, ClientPresenceState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<TResult> UpdateAsync<TResult>(
        string clientId,
        Func<ISet<string>, Task<TResult>> update,
        CancellationToken ct = default)
    {
        var state = _states.GetOrAdd(clientId, _ => new ClientPresenceState());
        await state.Gate.WaitAsync(ct);
        try
        {
            return await update(state.ConnectionIds);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public void Clear() => _states.Clear();

    private sealed class ClientPresenceState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public HashSet<string> ConnectionIds { get; } = new(StringComparer.Ordinal);
    }
}
