using System.Collections.Concurrent;
using System.Reflection;
using Bolt.Hub.Health;
using Bolt.Hub.Installers;
using Bolt.Protocol.Transport;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltServerTransportHealthTests
{
    [Test]
    public void GetHealthSnapshot_EmptyServer_ReportsEffectiveBoundsWithoutRuntimeFailures()
    {
        var options = new BoltServerOptions
        {
            InvocationTimeoutMs = 1_234,
            MaxFrameBytes = 16_384,
            SendQueueCapacity = 17,
            SendEnqueueTimeoutMs = 0,
            MaxPendingRpcCalls = 19,
            MaxPendingRpcCallsPerPrincipal = 2,
            MaxConnectionsPerPrincipal = 3,
            MaxActiveStreamsPerPrincipal = 5,
            MaxMediaStreamsPerPrincipal = 7,
            MaxSubscriptionsPerPrincipal = 11,
            MaxDurableSubscribersPerTopic = 13,
            MediaEnabled = false
        };
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, options);

        var snapshot = server.GetHealthSnapshot();

        snapshot.AcceptedConnections.Should().Be(0);
        snapshot.RegisteredConnections.Should().Be(0);
        snapshot.UnregisteredConnections.Should().Be(0);
        snapshot.LiveConnections.Should().Be(0);
        snapshot.PendingRpcCalls.Should().Be(0);
        snapshot.ActiveLogicalStreams.Should().Be(0);
        snapshot.ActiveCalls.Should().Be(0);
        snapshot.ActiveSubscriptionReservations.Should().Be(0);
        snapshot.AggregateQueuedSendBytes.Should().Be(0);
        snapshot.RunningSendLoops.Should().Be(0);
        snapshot.FaultedSendLoops.Should().Be(0);
        snapshot.IsDisposed.Should().BeFalse();
        snapshot.ConfiguredBounds.Should().Be(new BoltServerHealthBounds(
            MaximumFrameBytes: 16_384,
            SendQueueCapacityPerConnection: 17,
            SendEnqueueTimeoutMilliseconds: 1_234,
            SendBackpressureDropThresholdBytes: BoltHubConnection.BackpressureDropThreshold,
            SendBackpressureFeedbackThresholdBytes: BoltHubConnection.BackpressureFeedbackThreshold,
            MaximumPendingRpcCalls: 19,
            MaximumPendingRpcCallsPerPrincipal: 2,
            MaximumConnectionsPerPrincipal: 3,
            MaximumLogicalStreamsPerPrincipal: 5,
            MaximumMediaStreamsPerPrincipal: 7,
            MaximumSubscriptionsPerPrincipal: 11,
            MaximumDurableSubscribersPerTopic: 13,
            MediaEnabled: false));
    }

    [Test]
    public void GetHealthSnapshot_LiveConnection_ReportsQueueAndFaultedSendLoopWithoutIdentityData()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        var connection = new BoltHubConnection(new TestBoltConnection(isConnected: true))
        {
            ClientId = "secret-client-id",
            ClientName = "secret-client-name",
            IsRegistered = true
        };
        SetPrivateField(connection, "_pendingBytes", 2_048L);
        var faultedSendLoop = Task.FromException(new InvalidOperationException("synthetic send-loop failure"));
        _ = faultedSendLoop.Exception;
        SetPrivateProperty(connection, nameof(BoltHubConnection.SendLoop), faultedSendLoop);
        AddTrackedConnection(server, connection);

        var snapshot = server.GetHealthSnapshot();

        snapshot.AcceptedConnections.Should().Be(1);
        snapshot.RegisteredConnections.Should().Be(1);
        snapshot.LiveConnections.Should().Be(1);
        snapshot.AggregateQueuedSendBytes.Should().Be(2_048);
        snapshot.MaximumQueuedSendBytes.Should().Be(2_048);
        snapshot.CompletedSendLoops.Should().Be(1);
        snapshot.FaultedSendLoops.Should().Be(1);
        snapshot.LiveConnectionsWithInactiveSendLoops.Should().Be(1);
        snapshot.ToString().Should().NotContain("secret-client");

        SetPrivateField(connection, "_pendingBytes", 0L);
    }

    [Test]
    public async Task HealthCheck_LiveInactiveSendLoop_IsUnhealthy()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        AddTrackedConnection(server, new BoltHubConnection(new TestBoltConnection(isConnected: true))
        {
            IsRegistered = true
        });
        var check = new BoltTransportHealthCheck(server);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("live connection has inactive send loop");
        result.Data.Should().ContainKey("transport");
    }

    [Test]
    public async Task HealthCheck_DisconnectedInactiveSendLoop_RemainsHealthyAndObservable()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        AddTrackedConnection(server, new BoltHubConnection(new TestBoltConnection(isConnected: false))
        {
            IsRegistered = true
        });
        var check = new BoltTransportHealthCheck(server);

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        var snapshot = result.Data["transport"].Should().BeOfType<BoltServerHealthSnapshot>().Subject;

        result.Status.Should().Be(HealthStatus.Healthy);
        snapshot.RegisteredConnections.Should().Be(1);
        snapshot.LiveConnections.Should().Be(0);
        snapshot.LiveConnectionsWithInactiveSendLoops.Should().Be(0);
    }

    [Test]
    public async Task HealthCheck_ClosingLiveConnectionWithCompletedSendLoop_RemainsHealthy()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        var connection = new BoltHubConnection(new TestBoltConnection(isConnected: true))
        {
            IsRegistered = true
        };
        SetPrivateProperty(connection, nameof(BoltHubConnection.SendLoop), Task.CompletedTask);
        InvokePrivateMethod(connection, "BeginClose");
        AddTrackedConnection(server, connection);
        var check = new BoltTransportHealthCheck(server);

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        var snapshot = result.Data["transport"].Should().BeOfType<BoltServerHealthSnapshot>().Subject;

        result.Status.Should().Be(HealthStatus.Healthy);
        snapshot.ClosingConnections.Should().Be(1);
        snapshot.CompletedSendLoops.Should().Be(1);
        snapshot.LiveConnectionsWithInactiveSendLoops.Should().Be(0);
    }

    [Test]
    public async Task HealthCheck_LiveUnregisteredTransportWithRunningSendLoop_RemainsHealthy()
    {
        using var server = new BoltServer(NullLogger<BoltServer>.Instance, new BoltServerOptions());
        var connection = new BoltHubConnection(new TestBoltConnection(isConnected: true));
        connection.StartSendLoop(CancellationToken.None);
        AddAcceptedConnection(server, connection);
        var check = new BoltTransportHealthCheck(server);

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        var snapshot = result.Data["transport"].Should().BeOfType<BoltServerHealthSnapshot>().Subject;

        result.Status.Should().Be(HealthStatus.Healthy);
        snapshot.AcceptedConnections.Should().Be(1);
        snapshot.RegisteredConnections.Should().Be(0);
        snapshot.UnregisteredConnections.Should().Be(1);
        snapshot.RunningSendLoops.Should().Be(1);

        connection.CompleteSendChannel();
        await connection.SendLoop!.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task HealthCheck_ExceededPrincipalQuota_IsUnhealthy()
    {
        using var server = new BoltServer(
            NullLogger<BoltServer>.Instance,
            new BoltServerOptions { MaxConnectionsPerPrincipal = 1 });
        GetPrivateField<ConcurrentDictionary<string, int>>(server, "_connectionCountsByPrincipal")
            ["principal:test"] = 2;
        var check = new BoltTransportHealthCheck(server);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("principal connection limit exceeded");
    }

    [Test]
    public void BoltInstaller_RegistersTransportReadinessCheck()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        new BoltInstaller().InstallServices<BoltServerTransportHealthTests>(
            services,
            configuration,
            new TestHostEnvironment());
        using var provider = services.BuildServiceProvider();

        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.Single(item => item.Name == "Bolt-transport");

        registration.FailureStatus.Should().Be(HealthStatus.Unhealthy);
        registration.Tags.Should().Contain(["bolt", "transport", "ready"]);
    }

    private static void AddTrackedConnection(BoltServer server, BoltHubConnection connection)
    {
        AddAcceptedConnection(server, connection);
        GetPrivateField<ConcurrentDictionary<string, BoltHubConnection>>(server, "_connectionsByStreamId")
            [connection.StreamId] = connection;
    }

    private static void AddAcceptedConnection(BoltServer server, BoltHubConnection connection) =>
        GetPrivateField<ConcurrentDictionary<string, BoltHubConnection>>(server, "_activeTransportConnections")
            [connection.StreamId] = connection;

    private static T GetPrivateField<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, name));

    private static void SetPrivateField(object instance, string name, object value) =>
        (instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
         ?? throw new MissingFieldException(instance.GetType().FullName, name)).SetValue(instance, value);

    private static void SetPrivateProperty(object instance, string name, object value) =>
        (instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
         ?? throw new MissingMemberException(instance.GetType().FullName, name)).SetValue(instance, value);

    private static void InvokePrivateMethod(object instance, string name) =>
        (instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
         ?? throw new MissingMethodException(instance.GetType().FullName, name)).Invoke(instance, null);

    private sealed class TestBoltConnection(bool isConnected) : IBoltConnection
    {
        public bool SupportsDatagrams => false;
        public bool IsConnected { get; } = isConnected;
        public BoltTransport TransportType => BoltTransport.WebSocket;

        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken ct = default) =>
            ValueTask.FromResult((0, true));

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Bolt.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
