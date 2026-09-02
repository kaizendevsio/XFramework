namespace XFramework.Portal.Shared;

public interface IPortalActorContext
{
    Guid? CredentialId { get; }
    Guid? SessionId { get; }

    ValueTask<string?> GetActorAccessTokenAsync(CancellationToken cancellationToken = default);
}
