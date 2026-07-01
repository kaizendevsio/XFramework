namespace XFramework.Integration.Security;

public interface IServiceTokenProvider
{
    ValueTask<string> GetTokenAsync(
        string audience,
        IReadOnlyCollection<string>? scopes = null,
        CancellationToken ct = default);
}
