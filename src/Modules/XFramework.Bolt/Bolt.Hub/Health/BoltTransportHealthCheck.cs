using Bolt.Server;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bolt.Hub.Health;

/// <summary>
/// Readiness check for actionable Bolt transport invariant violations.
/// Queue pressure and disconnected connections remain observable without causing transient readiness failures.
/// </summary>
public sealed class BoltTransportHealthCheck(BoltServer server) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = server.GetHealthSnapshot();
        var violations = GetViolations(snapshot);
        IReadOnlyDictionary<string, object> data = new Dictionary<string, object>
        {
            ["transport"] = snapshot
        };

        return Task.FromResult(violations.Count == 0
            ? HealthCheckResult.Healthy("Bolt transport invariants are satisfied.", data)
            : HealthCheckResult.Unhealthy(
                $"Bolt transport invariant violation(s): {string.Join(", ", violations)}.",
                data: data));
    }

    private static List<string> GetViolations(BoltServerHealthSnapshot snapshot)
    {
        var bounds = snapshot.ConfiguredBounds;
        var violations = new List<string>();

        if (snapshot.IsDisposed)
            violations.Add("server disposed");
        if (snapshot.UnregisteredTrackedConnections > 0)
            violations.Add("unregistered connection present in registered connection index");
        if (snapshot.NegativeRuntimeCounters > 0)
            violations.Add("negative runtime counter");
        if (snapshot.LiveConnectionsWithInactiveSendLoops > 0)
            violations.Add("live connection has inactive send loop");
        if (snapshot.PendingRpcCalls > bounds.MaximumPendingRpcCalls)
            violations.Add("global pending RPC limit exceeded");
        if (snapshot.MaximumConnectionsForOnePrincipal > bounds.MaximumConnectionsPerPrincipal)
            violations.Add("principal connection limit exceeded");
        if (snapshot.MaximumPendingRpcCallsForOnePrincipal > bounds.MaximumPendingRpcCallsPerPrincipal)
            violations.Add("principal pending RPC limit exceeded");
        if (snapshot.MaximumLogicalStreamsForOnePrincipal > bounds.MaximumLogicalStreamsPerPrincipal)
            violations.Add("principal logical stream limit exceeded");
        if (snapshot.MaximumMediaStreamsForOnePrincipal > bounds.MaximumMediaStreamsPerPrincipal)
            violations.Add("principal media stream limit exceeded");
        if (snapshot.MaximumSubscriptionsForOnePrincipal > bounds.MaximumSubscriptionsPerPrincipal)
            violations.Add("principal subscription limit exceeded");

        return violations;
    }
}
