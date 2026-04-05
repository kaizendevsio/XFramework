using Grpc.Core;
using IdentityServer.Benchmarks.Grpc;

namespace IdentityServer.Benchmarks;

/// <summary>
/// gRPC backend — handles the actual health check.
/// Same role as IdentityServer in the Bolt benchmark.
/// </summary>
public class GrpcHealthBackend : HealthService.HealthServiceBase
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

/// <summary>
/// gRPC hub — proxies requests to the backend, like Bolt's BoltServer.
/// Client → Hub → Backend → Hub → Client (same hop count as Bolt).
/// </summary>
public class GrpcHealthHub : HealthService.HealthServiceBase
{
    private readonly HealthService.HealthServiceClient _backend;

    public GrpcHealthHub(HealthService.HealthServiceClient backend)
    {
        _backend = backend;
    }

    public override async Task<HealthCheckResp> Check(HealthCheckReq request, ServerCallContext context)
    {
        // Forward to backend — same as Bolt hub forwarding to recipient
        return await _backend.CheckAsync(request);
    }
}
