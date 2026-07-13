namespace Bolt.Server;

/// <summary>
/// Nonsecret, point-in-time transport state for health checks and canary observation.
/// Counts can change immediately after the snapshot is taken.
/// </summary>
public sealed record BoltServerHealthSnapshot(
    int AcceptedConnections,
    int RegisteredConnections,
    int UnregisteredConnections,
    int LiveConnections,
    int ClosingConnections,
    int UnregisteredTrackedConnections,
    int PendingRpcCalls,
    int ActiveLogicalStreams,
    int ActiveMediaStreams,
    int ActiveCalls,
    long ActiveSubscriptionReservations,
    long LiveTransientSubscriptions,
    int LiveDurableSubscriptions,
    long AggregateQueuedSendBytes,
    long MaximumQueuedSendBytes,
    int ConnectionsUnderSendPressure,
    int RunningSendLoops,
    int CompletedSendLoops,
    int FaultedSendLoops,
    int LiveConnectionsWithInactiveSendLoops,
    int NegativeRuntimeCounters,
    int MaximumConnectionsForOnePrincipal,
    int MaximumPendingRpcCallsForOnePrincipal,
    int MaximumLogicalStreamsForOnePrincipal,
    int MaximumMediaStreamsForOnePrincipal,
    int MaximumSubscriptionsForOnePrincipal,
    bool IsDisposed,
    BoltServerHealthBounds ConfiguredBounds);

/// <summary>
/// Effective server limits associated with a <see cref="BoltServerHealthSnapshot"/>.
/// </summary>
public sealed record BoltServerHealthBounds(
    int MaximumFrameBytes,
    int SendQueueCapacityPerConnection,
    int SendEnqueueTimeoutMilliseconds,
    long SendBackpressureDropThresholdBytes,
    long SendBackpressureFeedbackThresholdBytes,
    int MaximumPendingRpcCalls,
    int MaximumPendingRpcCallsPerPrincipal,
    int MaximumConnectionsPerPrincipal,
    int MaximumLogicalStreamsPerPrincipal,
    int MaximumMediaStreamsPerPrincipal,
    int MaximumSubscriptionsPerPrincipal,
    int MaximumDurableSubscribersPerTopic,
    bool MediaEnabled);
