namespace XFramework.Integration.Security;

public interface IActorAccessTokenProvider
{
    ValueTask<string?> GetTokenAsync(CancellationToken ct = default);
}

public interface IActorAccessTokenScope
{
    IDisposable Push(string actorAccessToken);
}

internal sealed class AmbientActorAccessTokenProvider : IActorAccessTokenProvider, IActorAccessTokenScope
{
    private static readonly AsyncLocal<Holder?> Current = new();

    public ValueTask<string?> GetTokenAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(Current.Value?.Token);

    public IDisposable Push(string actorAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorAccessToken);
        var prior = Current.Value;
        Current.Value = new Holder(actorAccessToken);
        return new PopScope(prior);
    }

    private sealed record Holder(string Token);

    private sealed class PopScope(Holder? prior) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            Current.Value = prior;
            _disposed = true;
        }
    }
}
