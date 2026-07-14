namespace XFramework.Integration.Security;

public interface IBoltTransportTokenProvider
{
    ValueTask<string> GetTokenAsync(CancellationToken ct = default);
}
