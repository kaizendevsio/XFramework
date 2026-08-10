namespace XFramework.Integration.Security;

public sealed record TrustedServiceAccessToken(
    string Token,
    string ClientId,
    string Audience,
    IReadOnlySet<string> Scopes);

public interface ITrustedServiceAccessTokenAccessor
{
    TrustedServiceAccessToken? Current { get; }
}

public interface ITrustedServiceAccessTokenStore : ITrustedServiceAccessTokenAccessor
{
    void Set(TrustedServiceAccessToken credential);
}

internal sealed class TrustedServiceAccessTokenContext : ITrustedServiceAccessTokenStore
{
    public TrustedServiceAccessToken? Current { get; private set; }

    public void Set(TrustedServiceAccessToken credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        if (Current is null)
        {
            Current = credential;
            return;
        }

        if (string.Equals(Current.Token, credential.Token, StringComparison.Ordinal) &&
            string.Equals(Current.ClientId, credential.ClientId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Current.Audience, credential.Audience, StringComparison.OrdinalIgnoreCase) &&
            Current.Scopes.SetEquals(credential.Scopes))
        {
            return;
        }

        throw new InvalidOperationException(
            "A trusted service access token has already been established for this scope.");
    }
}
