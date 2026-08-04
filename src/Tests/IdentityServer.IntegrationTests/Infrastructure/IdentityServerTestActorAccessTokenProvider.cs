using XFramework.Integration.Security;

namespace IdentityServer.IntegrationTests.Infrastructure;

internal sealed class IdentityServerTestActorAccessTokenProvider : IActorAccessTokenProvider
{
    private static readonly AsyncLocal<TokenOverride?> CurrentToken = new();

    public ValueTask<string?> GetTokenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CurrentToken.Value is { } current
            ? current.Token
            : IntegrationTestFixture.TestActorAccessToken);
    }

    public static IDisposable Push(string actorAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorAccessToken);
        var previous = CurrentToken.Value;
        CurrentToken.Value = new TokenOverride(actorAccessToken);
        return new TokenScope(previous);
    }

    public static IDisposable Suppress()
    {
        var previous = CurrentToken.Value;
        CurrentToken.Value = new TokenOverride(null);
        return new TokenScope(previous);
    }

    private sealed class TokenScope(TokenOverride? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            CurrentToken.Value = previous;
            _disposed = true;
        }
    }

    private sealed record TokenOverride(string? Token);
}
