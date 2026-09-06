using XFramework.Integration.Security;

namespace XFramework.Portal.Services;

public sealed class PortalActorAccessTokenScope : IActorAccessTokenScope
{
    private readonly AsyncLocal<Holder?> _current = new();

    internal bool TryGetToken(out string? token)
    {
        if (_current.Value is not { } current)
        {
            token = null;
            return false;
        }

        token = current.Token;
        return true;
    }

    public IDisposable Push(string actorAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorAccessToken);
        return Set(actorAccessToken);
    }

    public IDisposable Suppress() => Set(null);

    private IDisposable Set(string? token)
    {
        var prior = _current.Value;
        _current.Value = new Holder(token);
        return new PopScope(_current, prior);
    }

    private sealed record Holder(string? Token);

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
