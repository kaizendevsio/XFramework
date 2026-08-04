using XFramework.Domain.Shared.Security;

namespace XFramework.Integration.Security;

internal sealed class CrossTenantWriteAuthorization :
    ICrossTenantWriteAuthorizationAccessor,
    ICrossTenantWriteAuthorizationScopeFactory
{
    private readonly AsyncLocal<AuthorizationScope?> _current = new();

    public bool IsAuthorized => _current.Value is not null;

    public IDisposable BeginTenantAdministrationScope()
    {
        if (_current.Value is not null)
            throw new InvalidOperationException("A cross-tenant write authorization scope is already active.");

        var scope = new AuthorizationScope(this);
        _current.Value = scope;
        return scope;
    }

    private sealed class AuthorizationScope(CrossTenantWriteAuthorization owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (ReferenceEquals(owner._current.Value, this))
                owner._current.Value = null;
        }
    }
}
