using XFramework.Integration.Security;

namespace XFramework.Portal.Services;

public sealed class PortalActorAccessTokenProvider(PortalActorContext actorContext)
    : IActorAccessTokenProvider, IActorAccessTokenScope
{
    private readonly AsyncLocal<Holder?> _current = new();

    public ValueTask<string?> GetTokenAsync(CancellationToken ct = default) =>
        _current.Value is { } current
            ? ValueTask.FromResult<string?>(current.Token)
            : actorContext.GetActorAccessTokenAsync(ct);

    public IDisposable Push(string actorAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorAccessToken);
        var prior = _current.Value;
        _current.Value = new Holder(actorAccessToken);
        return new PopScope(_current, prior);
    }

    private sealed record Holder(string Token);

    private sealed class PopScope(AsyncLocal<Holder?> current, Holder? prior) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            current.Value = prior;
            _disposed = true;
        }
    }
}
