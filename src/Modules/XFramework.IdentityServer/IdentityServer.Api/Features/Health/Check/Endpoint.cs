using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Health.Check;

public static class HealthCheckEndpoint
{
    [BoltHandler]
    [MapPost("/api/health/check", Tags = ["Health"],
        Summary = "Health check",
        Description = "Lightweight health check for transport throughput benchmarking.")]
    public static Task<Result<HealthCheckResponse>> Handle(
        HealthCheckRequest request,
        CancellationToken ct)
    {
        var response = new HealthCheckResponse
        {
            Status = "ok",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        return Task.FromResult(Result<HealthCheckResponse>.Success(response));
    }
}
