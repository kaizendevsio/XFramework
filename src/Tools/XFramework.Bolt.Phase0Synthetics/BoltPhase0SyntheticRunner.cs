using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Communications.Domain.Shared.Contracts.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using global::Bolt.Client;

namespace XFramework.Bolt.Phase0Synthetics;

public sealed class BoltPhase0SyntheticRunner
{
    private const string CommunicationsServiceName = "XFramework.Communications";
    private const string PortalServiceName = "XFramework.Portal";
    private const string IdentityServerServiceName = "XFramework.IdentityServer";
    private const int DurableMessageCount = 3;

    public async Task<SyntheticReport> RunAsync(SyntheticOptions options, CancellationToken ct = default)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var operations = new List<SyntheticOperationResult>();
        var recorder = new OperationRecorder(operations, options.OperationTimeout);
        var tokenEvidence = CreateTokenEvidence(options);

        BoltClient? userClient = null;
        BoltClient? communicationsClient = null;
        IAsyncEnumerator<CommunicationsPresenceState>? transientEnumerator = null;
        CancellationTokenSource? transientCts = null;
        IAsyncEnumerator<DurableMessage<CommunicationsRealtimeEvent>>? durableReplayEnumerator = null;
        CancellationTokenSource? durableReplayCts = null;
        var durableAttempted = false;
        var durableUnregistered = false;
        var coreCompleted = false;
        var userTopic = CommunicationsAddressing.UserTopic(options.TenantId, options.CredentialId);
        var subscriberId = CommunicationsAddressing.DurableSubscriberId(
            options.TenantId,
            options.CredentialId,
            options.DeviceId,
            runId);

        try
        {
            userClient = CreatePortalClient(options);
            await recorder.RunAsync(
                "user_registration",
                async operationCt =>
                {
                    await userClient.ConnectAsync(operationCt);
                    EnsureConnected(userClient);
                    return Results(
                        ("portal_transport_authenticated", true),
                        ("portal_service_registered", true),
                        ("actor_token_separate", true));
                },
                ct);

            await recorder.RunAsync(
                "hostile_reserved_registration",
                async operationCt =>
                {
                    await ValidateReservedServiceRegistrationRejectedAsync(options, operationCt);
                    return Results(("authenticated_user_rejected", true), ("reserved_identity_protected", true));
                },
                ct);

            communicationsClient = CreateClient(
                options,
                Sha256Hex(CommunicationsServiceName),
                CommunicationsServiceName,
                options.CommunicationsTransportToken);
            await recorder.RunAsync(
                "communications_registration",
                async operationCt =>
                {
                    await communicationsClient.ConnectAsync(operationCt);
                    EnsureConnected(communicationsClient);
                    return Results(("authenticated", true), ("registered", true));
                },
                ct);

            if (options.RejectedPortalTransportToken is not null)
            {
                await recorder.RunAsync(
                    "old_generation_portal_transport_token_rejection",
                    async operationCt =>
                    {
                        await ValidateTokenRegistrationRejectedAsync(
                            options,
                            Sha256Hex(PortalServiceName),
                            PortalServiceName,
                            options.RejectedPortalTransportToken,
                            operationCt);
                        return Results(("old_generation_rejected", true), ("portal_transport_token_rejected", true));
                    },
                    ct);
            }

            if (options.RejectedCommunicationsTransportToken is not null)
            {
                await recorder.RunAsync(
                    "old_generation_communications_transport_token_rejection",
                    async operationCt =>
                    {
                        await ValidateTokenRegistrationRejectedAsync(
                            options,
                            Sha256Hex(CommunicationsServiceName),
                            CommunicationsServiceName,
                            options.RejectedCommunicationsTransportToken,
                            operationCt);
                        return Results(("old_generation_rejected", true), ("service_token_rejected", true));
                    },
                    ct);
            }

            var presenceTopic = CommunicationsAddressing.PresenceTopic(options.TenantId);
            var expectedPresence = new CommunicationsPresenceState
            {
                TenantId = options.TenantId,
                CredentialId = options.CredentialId,
                IsOnline = true,
                LastActiveAt = DateTime.UtcNow
            };
            transientCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            transientCts.CancelAfter(options.OperationTimeout);
            transientEnumerator = userClient
                .SubscribeAsync<CommunicationsPresenceState>(
                    presenceTopic,
                    transientCts.Token,
                    options.UserActorToken.Reveal())
                .GetAsyncEnumerator(transientCts.Token);
            var presenceReceiveTask = ReceiveMatchingAsync(
                transientEnumerator,
                value => value == expectedPresence,
                transientCts.Token);

            await recorder.RunAsync(
                "identity_health_check",
                async operationCt =>
                {
                    await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                        userClient,
                        options,
                        options.PortalIdentityServiceToken,
                        operationCt);
                    return Results(("query_response_validated", true));
                },
                ct);

            await recorder.RunAsync(
                "transient_presence",
                async operationCt =>
                {
                    await communicationsClient.PublishAsync(
                        presenceTopic,
                        expectedPresence,
                        durable: false,
                        operationCt);
                    await presenceReceiveTask.WaitAsync(operationCt);
                    return Results(("published", true), ("received", true), ("transient", true));
                },
                ct);

            await transientEnumerator.DisposeAsync().AsTask().WaitAsync(options.OperationTimeout, ct);
            transientEnumerator = null;
            transientCts.Dispose();
            transientCts = null;

            durableAttempted = true;
            await recorder.RunAsync(
                "durable_offline_registration",
                async operationCt =>
                {
                    await RegisterAndDetachDurableAsync(
                        userClient,
                        userTopic,
                        subscriberId,
                        options,
                        operationCt);
                    return Results(("registered", true), ("detached", true), ("offline", true));
                },
                ct);

            await DisposeClientQuietlyAsync(userClient, options.OperationTimeout);
            userClient = null;

            var expectedEvents = Enumerable.Range(1, DurableMessageCount)
                .Select(ordinal => CreateDurableEvent(options, ordinal))
                .ToArray();
            await recorder.RunAsync(
                "durable_offline_publish",
                async operationCt =>
                {
                    foreach (var expectedEvent in expectedEvents)
                    {
                        await communicationsClient.PublishAsync(
                            userTopic,
                            expectedEvent,
                            durable: true,
                            operationCt);
                    }

                    await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                        communicationsClient,
                        options,
                        options.CommunicationsIdentityServiceToken,
                        operationCt);
                    return Results(("published_while_offline", true), ("batch_ordered", true));
                },
                ct);

            userClient = CreatePortalClient(options);
            var replayMessages = await recorder.RunAsync(
                "durable_ordered_replay",
                async operationCt =>
                {
                    await userClient.ConnectAsync(operationCt);
                    EnsureConnected(userClient);
                    durableReplayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    durableReplayEnumerator = userClient
                        .SubscribeDurableAsync<CommunicationsRealtimeEvent>(
                            userTopic,
                            subscriberId,
                            durableReplayCts.Token,
                            options.UserActorToken.Reveal())
                        .GetAsyncEnumerator(durableReplayCts.Token);
                    var received = await ReadOrderedReplayAsync(
                        durableReplayEnumerator,
                        expectedEvents,
                        operationCt);
                    return (
                        received,
                        Results(("reconnected", true), ("ordered_replay", true), ("replayed_all", true)));
                },
                ct);

            await recorder.RunAsync(
                "durable_ack",
                async operationCt =>
                {
                    var replayCts = durableReplayCts
                        ?? throw new SyntheticCheckException("durable_replay_session_missing");
                    var replayEnumerator = durableReplayEnumerator
                        ?? throw new SyntheticCheckException("durable_replay_session_missing");
                    var firstReplayMessage = replayMessages[0];
                    var lastReplayMessage = replayMessages[^1];
                    await lastReplayMessage.AckAsync(operationCt);
                    await firstReplayMessage.AckAsync(operationCt);
                    await lastReplayMessage.AckAsync(operationCt);
                    await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                        userClient,
                        options,
                        options.PortalIdentityServiceToken,
                        operationCt);
                    replayCts.Cancel();
                    await DisposeEnumeratorQuietlyAsync(replayEnumerator);
                    durableReplayEnumerator = null;
                    replayCts.Dispose();
                    durableReplayCts = null;
                    return Results(
                        ("cumulative_ack_sent", true),
                        ("stale_ack_sent", true),
                        ("duplicate_ack_sent", true),
                        ("processing_barrier_passed", true));
                },
                ct);

            await DisposeClientQuietlyAsync(userClient, options.OperationTimeout);
            userClient = CreatePortalClient(options);
            await recorder.RunAsync(
                "durable_no_redelivery",
                async operationCt =>
                {
                    await userClient.ConnectAsync(operationCt);
                    EnsureConnected(userClient);
                    await VerifyNoReplayAsync(userClient, userTopic, subscriberId, options, operationCt);
                    return Results(
                        ("reconnected", true),
                        ("acknowledgement_persisted", true),
                        ("no_redelivery", true));
                },
                ct);

            await recorder.RunAsync(
                "durable_unregister",
                async operationCt =>
                {
                    await userClient.UnregisterDurableSubscriptionWithActorAsync(
                        userTopic,
                        subscriberId,
                        options.UserActorToken.Reveal(),
                        operationCt);
                    await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                        userClient,
                        options,
                        options.PortalIdentityServiceToken,
                        operationCt);
                    await DisposeClientQuietlyAsync(userClient, options.OperationTimeout);
                    userClient = null;

                    var postUnregisterEvent = CreateDurableEvent(options, DurableMessageCount + 1);
                    await communicationsClient.PublishAsync(
                        userTopic,
                        postUnregisterEvent,
                        durable: true,
                        operationCt);
                    await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                        communicationsClient,
                        options,
                        options.CommunicationsIdentityServiceToken,
                        operationCt);

                    userClient = CreatePortalClient(options);
                    await userClient.ConnectAsync(operationCt);
                    EnsureConnected(userClient);
                    await VerifyNoReplayAsync(userClient, userTopic, subscriberId, options, operationCt);
                    await userClient.UnregisterDurableSubscriptionWithActorAsync(
                        userTopic,
                        subscriberId,
                        options.UserActorToken.Reveal(),
                        operationCt);
                    await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                        userClient,
                        options,
                        options.PortalIdentityServiceToken,
                        operationCt);
                    durableUnregistered = true;
                    return Results(("permanently_unregistered", true), ("post_unregister_not_queued", true));
                },
                ct);

            coreCompleted = true;
        }
        catch
        {
            coreCompleted = false;
        }
        finally
        {
            transientCts?.Cancel();
            if (transientEnumerator is not null)
                await DisposeEnumeratorQuietlyAsync(transientEnumerator);

            transientCts?.Dispose();

            durableReplayCts?.Cancel();
            if (durableReplayEnumerator is not null)
                await DisposeEnumeratorQuietlyAsync(durableReplayEnumerator);

            durableReplayCts?.Dispose();

            if (durableAttempted && !durableUnregistered)
            {
                try
                {
                    await recorder.RunAsync(
                        "durable_cleanup_unregister",
                        async operationCt =>
                        {
                            if (userClient?.IsConnected != true)
                            {
                                await DisposeClientQuietlyAsync(userClient, options.OperationTimeout);
                                userClient = CreatePortalClient(options);
                                await userClient.ConnectAsync(operationCt);
                                EnsureConnected(userClient);
                            }

                            await userClient.UnregisterDurableSubscriptionWithActorAsync(
                                userTopic,
                                subscriberId,
                                options.UserActorToken.Reveal(),
                                operationCt);
                            await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                                userClient,
                                options,
                                options.PortalIdentityServiceToken,
                                operationCt);
                            return Results(("cleanup_permanently_unregistered", true));
                        },
                        CancellationToken.None,
                        options.OperationTimeout);
                }
                catch
                {
                    coreCompleted = false;
                }
            }

            if (communicationsClient is not null)
                await DisposeClientQuietlyAsync(communicationsClient, options.OperationTimeout);

            if (userClient is not null)
                await DisposeClientQuietlyAsync(userClient, options.OperationTimeout);
        }

        if (coreCompleted && options.ExpiryTransportToken is not null)
        {
            try
            {
                await recorder.RunAsync(
                    "token_expiry_disconnect",
                    operationCt => ValidateExpiryDisconnectAsync(options, operationCt),
                    ct,
                    options.ExpiryMaxWait + options.ExpiryGrace);
            }
            catch
            {
                coreCompleted = false;
            }
        }

        var completedAtUtc = DateTimeOffset.UtcNow;
        return new SyntheticReport(
            SyntheticReportValidator.SchemaVersion,
            runId,
            tokenEvidence,
            startedAtUtc,
            completedAtUtc,
            options.Target.AbsoluteUri,
            coreCompleted && !recorder.HasFailures ? "passed" : "failed",
            new SyntheticTimings((long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
            operations);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ValidateExpiryDisconnectAsync(
        SyntheticOptions options,
        CancellationToken ct)
    {
        var token = options.ExpiryTransportToken!;
        var descriptor = JwtDescriptorReader.Read(token);
        var now = DateTimeOffset.UtcNow;
        var untilExpiration = descriptor.ExpiresAtUtc - now;
        if (untilExpiration < TimeSpan.FromSeconds(1) ||
            untilExpiration + options.ExpiryGrace > options.ExpiryMaxWait)
        {
            throw new SyntheticCheckException("expiry_outside_bounded_window");
        }

        if (!string.Equals(descriptor.ServiceName, CommunicationsServiceName, StringComparison.Ordinal))
            throw new SyntheticCheckException("expiry_transport_identity_mismatch");

        var clientName = CommunicationsServiceName;
        var clientId = Sha256Hex(CommunicationsServiceName);
        var client = CreateClient(options, clientId, clientName, token);
        try
        {
            var disconnected = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Disconnected += () => disconnected.TrySetResult(DateTimeOffset.UtcNow);
            await client.ConnectAsync(ct);
            EnsureConnected(client);

            var deadline = descriptor.ExpiresAtUtc + options.ExpiryGrace;
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new SyntheticCheckException("expiry_disconnect_deadline_elapsed");

            DateTimeOffset disconnectedAtUtc;
            try
            {
                disconnectedAtUtc = await disconnected.Task.WaitAsync(remaining, ct);
            }
            catch (TimeoutException)
            {
                throw new SyntheticCheckException("expiry_disconnect_not_observed");
            }

            if (disconnectedAtUtc < descriptor.ExpiresAtUtc - TimeSpan.FromSeconds(1) ||
                disconnectedAtUtc > deadline)
            {
                throw new SyntheticCheckException("expiry_disconnect_outside_grace");
            }

            return Results(("exp_claim_validated", true), ("disconnected_by_deadline", true));
        }
        finally
        {
            await DisposeClientQuietlyAsync(client, options.OperationTimeout);
        }
    }

    private static BoltClient CreateClient(
        SyntheticOptions options,
        string clientId,
        string clientName,
        SecretToken transportToken)
    {
        var timeoutSeconds = Math.Max(1, (int)Math.Ceiling(options.OperationTimeout.TotalSeconds));
        var clientOptions = new BoltClientOptions
        {
            AccessToken = transportToken.Reveal(),
            RpcTimeoutSeconds = timeoutSeconds,
            TransportAttemptTimeoutMs = Math.Min(timeoutSeconds * 1000, 30_000),
            MinConnections = 1,
            MaxConnections = 1,
            // Exercise the browser-compatible Serve ingress so the deployment
            // gate can prove query tokens are absent from tailscaled journals.
            SendAccessTokenAsQueryString = true
        };
        return new BoltClient(
            options.Target,
            clientId,
            clientName,
            clientOptions,
            NullLogger<BoltClient>.Instance);
    }

    private static BoltClient CreatePortalClient(SyntheticOptions options) =>
        CreateClient(
            options,
            Sha256Hex(PortalServiceName),
            PortalServiceName,
            options.PortalTransportToken);

    private static CommunicationsRealtimeEvent CreateDurableEvent(
        SyntheticOptions options,
        int ordinal) =>
        new()
        {
            EventId = Guid.NewGuid(),
            TenantId = options.TenantId,
            ActorCredentialId = options.CredentialId,
            EventType = "Phase0Synthetic",
            OccurredAt = DateTime.UtcNow,
            PayloadJson = $"{{\"ordinal\":{ordinal}}}"
        };

    private static async Task RegisterAndDetachDurableAsync(
        BoltClient client,
        string topic,
        string subscriberId,
        SyntheticOptions options,
        CancellationToken ct)
    {
        using var subscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var enumerator = client
            .SubscribeDurableAsync<CommunicationsRealtimeEvent>(
                topic,
                subscriberId,
                subscriptionCts.Token,
                options.UserActorToken.Reveal())
            .GetAsyncEnumerator(subscriptionCts.Token);
        Task<bool>? moveNext = null;
        try
        {
            moveNext = enumerator.MoveNextAsync().AsTask();
            await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                client,
                options,
                options.PortalIdentityServiceToken,
                ct);
            if (moveNext.IsCompleted && await moveNext)
                throw new SyntheticCheckException("unexpected_durable_message_before_offline_publish");
        }
        finally
        {
            subscriptionCts.Cancel();
            await CompletePendingMoveNextQuietlyAsync(moveNext);
            await DisposeEnumeratorQuietlyAsync(enumerator);
        }
    }

    private static async Task<IReadOnlyList<DurableMessage<CommunicationsRealtimeEvent>>> ReadOrderedReplayAsync(
        IAsyncEnumerator<DurableMessage<CommunicationsRealtimeEvent>> enumerator,
        IReadOnlyList<CommunicationsRealtimeEvent> expected,
        CancellationToken ct)
    {
        var received = new List<DurableMessage<CommunicationsRealtimeEvent>>(expected.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            if (!await enumerator.MoveNextAsync().AsTask().WaitAsync(ct))
                throw new SyntheticCheckException("durable_replay_ended_early");

            var current = enumerator.Current;
            if (!current.IsReplay ||
                current.Sequence <= 0 ||
                current.Payload.EventId != expected[index].EventId ||
                current.Payload.TenantId != expected[index].TenantId ||
                current.Payload.ActorCredentialId != expected[index].ActorCredentialId ||
                (received.Count > 0 && current.Sequence <= received[^1].Sequence))
            {
                throw new SyntheticCheckException("durable_replay_not_ordered");
            }

            received.Add(current);
        }

        return received;
    }

    private static async Task VerifyNoReplayAsync(
        BoltClient client,
        string topic,
        string subscriberId,
        SyntheticOptions options,
        CancellationToken ct)
    {
        using var subscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var enumerator = client
            .SubscribeDurableAsync<CommunicationsRealtimeEvent>(
                topic,
                subscriberId,
                subscriptionCts.Token,
                options.UserActorToken.Reveal())
            .GetAsyncEnumerator(subscriptionCts.Token);
        Task<bool>? moveNext = null;
        try
        {
            moveNext = enumerator.MoveNextAsync().AsTask();
            await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                client,
                options,
                options.PortalIdentityServiceToken,
                ct);
            var observation = Task.Delay(NoRedeliveryObservationWindow(options.OperationTimeout), ct);
            var completed = await Task.WhenAny(moveNext, observation);
            if (completed == moveNext)
            {
                if (await moveNext)
                    throw new SyntheticCheckException("acked_durable_message_redelivered");

                throw new SyntheticCheckException("durable_subscription_ended_during_observation");
            }

            await observation;
        }
        finally
        {
            subscriptionCts.Cancel();
            await CompletePendingMoveNextQuietlyAsync(moveNext);
            await DisposeEnumeratorQuietlyAsync(enumerator);
        }
    }

    private static async Task ValidateReservedServiceRegistrationRejectedAsync(
        SyntheticOptions options,
        CancellationToken ct)
    {
        var hostileClient = CreateClient(
            options,
            Sha256Hex(IdentityServerServiceName),
            IdentityServerServiceName,
            options.PortalTransportToken);
        try
        {
            try
            {
                await hostileClient.ConnectAsync(ct);
            }
            catch (InvalidOperationException ex) when (ContainsMessage(ex, "rejected registration"))
            {
                return;
            }

            throw new SyntheticCheckException("reserved_service_registration_not_rejected");
        }
        finally
        {
            await DisposeClientQuietlyAsync(hostileClient, options.OperationTimeout);
        }
    }

    private static async Task ValidateTokenRegistrationRejectedAsync(
        SyntheticOptions options,
        string clientId,
        string clientName,
        SecretToken token,
        CancellationToken ct)
    {
        var client = CreateClient(options, clientId, clientName, token);
        try
        {
            try
            {
                await client.ConnectAsync(ct);
            }
            catch (InvalidOperationException ex) when (ContainsMessage(ex, "rejected registration"))
            {
                return;
            }

            throw new SyntheticCheckException("old_generation_token_not_rejected");
        }
        finally
        {
            await DisposeClientQuietlyAsync(client, options.OperationTimeout);
        }
    }

    private static bool ContainsMessage(Exception exception, string value) =>
        exception.Message.Contains(value, StringComparison.OrdinalIgnoreCase) ||
        (exception.InnerException is not null && ContainsMessage(exception.InnerException, value));

    private static TimeSpan NoRedeliveryObservationWindow(TimeSpan operationTimeout) =>
        TimeSpan.FromMilliseconds(Math.Clamp(operationTimeout.TotalMilliseconds / 3, 250, 2_000));

    private static async Task CompletePendingMoveNextQuietlyAsync(Task<bool>? moveNext)
    {
        if (moveNext is null)
            return;

        try { await moveNext.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch { }
    }

    private static async Task DisposeEnumeratorQuietlyAsync<T>(IAsyncEnumerator<T> enumerator)
    {
        try { await enumerator.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)); }
        catch { }
    }

    private static async Task DisposeClientQuietlyAsync(BoltClient? client, TimeSpan timeout)
    {
        if (client is null)
            return;

        try { await client.DisposeAsync().AsTask().WaitAsync(timeout); }
        catch { }
    }

    private static async Task<T> ReceiveMatchingAsync<T>(
        IAsyncEnumerator<T> enumerator,
        Func<T, bool> predicate,
        CancellationToken ct)
    {
        while (await enumerator.MoveNextAsync().AsTask().WaitAsync(ct))
        {
            if (predicate(enumerator.Current))
                return enumerator.Current;
        }

        throw new SyntheticCheckException("subscription_ended_without_match");
    }

    private static void EnsureConnected(BoltClient client)
    {
        if (!client.IsConnected)
            throw new SyntheticCheckException("registration_not_connected");
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static IReadOnlyDictionary<string, string> CreateTokenEvidence(SyntheticOptions options)
    {
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["communications_transport"] = options.CommunicationsTransportToken.Sha256Prefix,
            ["communications_identity_service"] = options.CommunicationsIdentityServiceToken.Sha256Prefix,
            ["portal_transport"] = options.PortalTransportToken.Sha256Prefix,
            ["portal_identity_service"] = options.PortalIdentityServiceToken.Sha256Prefix,
            ["user_actor"] = options.UserActorToken.Sha256Prefix
        };
        if (options.ExpiryTransportToken is not null)
            evidence["expiry_transport"] = options.ExpiryTransportToken.Sha256Prefix;
        if (options.RejectedCommunicationsTransportToken is not null)
        {
            evidence["rejected_communications_transport"] =
                options.RejectedCommunicationsTransportToken.Sha256Prefix;
        }
        if (options.RejectedPortalTransportToken is not null)
            evidence["rejected_portal_transport"] = options.RejectedPortalTransportToken.Sha256Prefix;

        return evidence;
    }

    private static IReadOnlyDictionary<string, string> Results(params (string Name, bool Value)[] values) =>
        values.ToDictionary(
            static value => value.Name,
            static value => value.Value ? "true" : "false",
            StringComparer.Ordinal);

    private sealed class OperationRecorder(
        List<SyntheticOperationResult> operations,
        TimeSpan defaultTimeout)
    {
        public bool HasFailures { get; private set; }

        public Task<IReadOnlyDictionary<string, string>> RunAsync(
            string name,
            Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>> action,
            CancellationToken ct,
            TimeSpan? timeout = null) =>
            RunCoreAsync(name, action, ct, timeout);

        public async Task<T> RunAsync<T>(
            string name,
            Func<CancellationToken, Task<(T Value, IReadOnlyDictionary<string, string> Results)>> action,
            CancellationToken ct,
            TimeSpan? timeout = null)
        {
            T? value = default;
            await RunCoreAsync(
                name,
                async operationCt =>
                {
                    var result = await action(operationCt);
                    value = result.Value;
                    return result.Results;
                },
                ct,
                timeout);
            return value!;
        }

        private async Task<IReadOnlyDictionary<string, string>> RunCoreAsync(
            string name,
            Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>> action,
            CancellationToken ct,
            TimeSpan? timeout)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout ?? defaultTimeout);
            try
            {
                var results = await action(timeoutCts.Token);
                stopwatch.Stop();
                operations.Add(new SyntheticOperationResult(
                    name,
                    startedAtUtc,
                    DateTimeOffset.UtcNow,
                    "passed",
                    stopwatch.ElapsedMilliseconds,
                    results));
                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                HasFailures = true;
                operations.Add(new SyntheticOperationResult(
                    name,
                    startedAtUtc,
                    DateTimeOffset.UtcNow,
                    "failed",
                    stopwatch.ElapsedMilliseconds,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["outcome"] = FailureCode(ex, timeoutCts.IsCancellationRequested, ct.IsCancellationRequested)
                    }));
                throw;
            }
        }

        private static string FailureCode(Exception exception, bool operationCancelled, bool callerCancelled) =>
            exception switch
            {
                SyntheticCheckException check => check.Code,
                SyntheticConfigurationException configuration => configuration.Code,
                TimeoutException => "timeout",
                OperationCanceledException when callerCancelled => "cancelled",
                OperationCanceledException when operationCancelled => "timeout",
                _ => "operation_failed"
            };
    }
}
