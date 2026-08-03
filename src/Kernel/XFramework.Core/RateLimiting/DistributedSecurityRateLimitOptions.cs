using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace XFramework.Core.RateLimiting;

public sealed class DistributedSecurityRateLimitOptions
{
    public const string SectionName = "DistributedSecurityRateLimiting";

    public bool Enabled { get; set; } = true;

    public string? RedisConnectionString { get; set; }

    public string KeyPrefix { get; set; } = "xframework:identity:strict-rate-limit";

    public int ConnectTimeoutMilliseconds { get; set; } = 3_000;

    public int OperationTimeoutMilliseconds { get; set; } = 1_000;
}

public sealed class DistributedSecurityRateLimitOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<DistributedSecurityRateLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, DistributedSecurityRateLimitOptions options)
    {
        var permitsDisabledMode = environment.IsDevelopment()
            || environment.IsEnvironment("Test")
            || environment.IsEnvironment("Testing");

        if (!options.Enabled)
        {
            return permitsDisabledMode
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    "Distributed security rate limiting may only be disabled in Development or Test environments.");
        }

        if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            return ValidateOptionsResult.Fail(
                "A Redis connection string is required when distributed security rate limiting is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyPrefix))
            return ValidateOptionsResult.Fail("A distributed security rate-limit key prefix is required.");

        if (options.ConnectTimeoutMilliseconds <= 0 || options.OperationTimeoutMilliseconds <= 0)
            return ValidateOptionsResult.Fail("Distributed security rate-limit timeouts must be positive.");

        return ValidateOptionsResult.Success;
    }
}
