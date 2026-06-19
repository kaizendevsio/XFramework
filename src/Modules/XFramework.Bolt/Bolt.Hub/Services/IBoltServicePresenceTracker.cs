namespace Bolt.Hub.Services;

public interface IBoltServicePresenceTracker
{
    Task<TResult> UpdateAsync<TResult>(
        string clientId,
        Func<ISet<string>, Task<TResult>> update,
        CancellationToken ct = default);

    void Clear();
}
