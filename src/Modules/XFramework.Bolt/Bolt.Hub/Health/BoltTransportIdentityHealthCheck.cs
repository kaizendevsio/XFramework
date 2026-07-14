using Bolt.Hub.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bolt.Hub.Health;

public sealed class BoltTransportIdentityHealthCheck(
    IOptionsMonitor<JwtBearerOptions> bearerOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = bearerOptions.Get(BoltTransportAuthentication.Scheme);
            var configurationManager = options.ConfigurationManager;
            if (configurationManager is null)
                return HealthCheckResult.Unhealthy("Bolt transport metadata manager is unavailable.");

            var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);
            if (!string.Equals(
                    configuration.Issuer,
                    BoltTransportAuthentication.ExpectedIssuer,
                    StringComparison.Ordinal))
            {
                return HealthCheckResult.Unhealthy("Bolt transport metadata issuer is invalid.");
            }

            var hasRsaSigningKey = configuration.SigningKeys.Any(static key => key switch
            {
                RsaSecurityKey rsa => rsa.KeySize >= 2048,
                JsonWebKey jsonWebKey =>
                    string.Equals(jsonWebKey.Kty, JsonWebAlgorithmsKeyTypes.RSA, StringComparison.Ordinal) &&
                    string.Equals(jsonWebKey.Use, "sig", StringComparison.Ordinal) &&
                    string.Equals(jsonWebKey.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(jsonWebKey.Kid) &&
                    !string.IsNullOrWhiteSpace(jsonWebKey.N) &&
                    !string.IsNullOrWhiteSpace(jsonWebKey.E) &&
                    jsonWebKey.KeySize >= 2048,
                _ => false
            });

            return hasRsaSigningKey
                ? HealthCheckResult.Healthy("Bolt transport identity metadata is available.")
                : HealthCheckResult.Unhealthy("Bolt transport metadata contains no usable RSA signing key.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Bolt transport identity metadata could not be resolved.",
                exception);
        }
    }
}
