using Grpc.Core;
using IdentityServer.Benchmarks.Grpc;

namespace IdentityServer.Benchmarks;

/// <summary>
/// gRPC HealthService implementation — benchmark-only, mirrors the same logic
/// as the HTTP and StreamFlow HealthCheck endpoints.
/// </summary>
public class GrpcHealthServiceImpl : HealthService.HealthServiceBase
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public override Task<HealthCheckResp> Check(HealthCheckReq request, ServerCallContext context)
    {
        return Task.FromResult(new HealthCheckResp
        {
            Status = "Healthy",
            StartTime = StartTime.ToString("O")
        });
    }
}
