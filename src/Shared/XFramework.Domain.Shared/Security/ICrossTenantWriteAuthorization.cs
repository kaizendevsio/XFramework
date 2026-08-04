namespace XFramework.Domain.Shared.Security;

public interface ICrossTenantWriteAuthorizationAccessor
{
    bool IsAuthorized { get; }
}

public interface ICrossTenantWriteAuthorizationScopeFactory
{
    IDisposable BeginTenantAdministrationScope();
}
