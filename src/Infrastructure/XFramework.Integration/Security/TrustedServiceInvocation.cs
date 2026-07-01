using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public sealed record TrustedServiceInvocation(
    string CallerClientId,
    string Audience,
    Guid? TenantId,
    Guid? ActorCredentialId,
    RequestMetadata Metadata,
    IReadOnlySet<string> Scopes);

public sealed record TrustedServiceInvocationResult(
    bool IsSuccess,
    TrustedServiceInvocation? Invocation,
    string? Error,
    int StatusCode)
{
    public static TrustedServiceInvocationResult Success(TrustedServiceInvocation invocation) =>
        new(true, invocation, null, 200);

    public static TrustedServiceInvocationResult Failure(string error, int statusCode = 401) =>
        new(false, null, error, statusCode);
}
