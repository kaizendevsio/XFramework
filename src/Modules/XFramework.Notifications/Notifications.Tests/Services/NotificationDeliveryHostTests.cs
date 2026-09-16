using System.Net;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Api.Services;
using Notifications.Api.Services.Push;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Enums;
using NUnit.Framework;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using SmsGateway.Integration.Drivers;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Security;
using XFramework.Integration.Security;

namespace Notifications.Tests.Services;

/// <summary>
/// Covers the background loop the way it actually runs: a scope with no tenant of its own.
///
/// Every earlier dispatcher test handed the DbContext an accessor that claimed a trusted tenant
/// unconditionally, so the suite was green while production threw on the first query of every poll
/// and no queued message push ever left the table. These tests start from an untrusted scope, which
/// is what a BackgroundService really gets.
/// </summary>
public sealed class NotificationDeliveryHostTests
{
    private const string P256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string Auth = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string VapidPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string VapidPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";

    /// <summary>
    /// The bug: a message push is queued, the hosted service runs, and nothing reaches the push
    /// service because the dispatcher never established a tenant for its scope.
    /// </summary>
    [Test]
    public async Task HostedService_DeliversAQueuedMessagePush_FromAScopeWithNoTenant()
    {
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        await using var harness = await DeliveryHarness.CreateAsync(handler);
        var tenantId = Guid.NewGuid();
        var jobId = await harness.QueueMessagePushAsync(tenantId);

        await harness.RunHostedServiceAsync(() => handler.Requests.Count > 0);

        handler.Requests.Should().ContainSingle("the queued message push must reach the push service");
        harness.Establishments.Should().Contain(tenantId, "the dispatcher must run under the job's tenant");

        var job = await harness.ReadJobAsync(jobId);
        job.Status.Should().Be(NotificationDeliveryStatus.Sent);
        job.ProviderMessageId.Should().Be("web-push:1");
    }

    /// <summary>
    /// The mechanism, isolated: discovery runs before any tenant is known, so it must not depend on
    /// the tenant query filter. A tenantless scope resolves CurrentTenantId to Guid.Empty, which
    /// matches no row.
    /// </summary>
    [Test]
    public async Task FindDueTenantIdsAsync_FromATenantlessScope_SeesEveryTenantWithWork()
    {
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        await using var harness = await DeliveryHarness.CreateAsync(handler);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await harness.QueueMessagePushAsync(first);
        await harness.QueueMessagePushAsync(second);

        await using var scope = harness.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopeTenantContext>().Establish(null);

        var tenants = await scope.ServiceProvider
            .GetRequiredService<NotificationDeliveryDispatcher>()
            .FindDueTenantIdsAsync(CancellationToken.None);

        tenants.Should().BeEquivalentTo([first, second]);
    }

    /// <summary>
    /// Pins the failure mode. A scope that never established a tenant cannot read a tenant-owned
    /// table at all - the filter throws rather than returning nothing - so a dispatcher that skips
    /// the establish step fails on its first query on every poll and delivers nothing, forever.
    /// </summary>
    [Test]
    public async Task DispatchDueAsync_WithoutAnEstablishedTenant_Throws()
    {
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        await using var harness = await DeliveryHarness.CreateAsync(handler);
        await harness.QueueMessagePushAsync(Guid.NewGuid());

        await using var scope = harness.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDeliveryDispatcher>();

        await FluentActions.Awaiting(() => dispatcher.DispatchDueAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trusted tenant context*");

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A tenant pass must stay inside its own tenant even though the table is shared.</summary>
    [Test]
    public async Task DispatchDueAsync_UnderOneTenant_LeavesAnotherTenantsJobQueued()
    {
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        await using var harness = await DeliveryHarness.CreateAsync(handler);
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        await harness.QueueMessagePushAsync(mine);
        var untouched = await harness.QueueMessagePushAsync(theirs);

        await using (var scope = harness.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ScopeTenantContext>().Establish(mine);
            var processed = await scope.ServiceProvider
                .GetRequiredService<NotificationDeliveryDispatcher>()
                .DispatchDueAsync(CancellationToken.None);
            processed.Should().Be(1);
        }

        handler.Requests.Should().ContainSingle();
        (await harness.ReadJobAsync(untouched)).Status.Should().Be(NotificationDeliveryStatus.Queued);
    }

    /// <summary>
    /// Harness that mirrors production wiring: one scoped tenant context per scope, untrusted until
    /// something establishes it, and a DbContext built from that scope's accessor.
    /// </summary>
    private sealed class DeliveryHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<AppDbContext> options;

        private DeliveryHarness(SqliteConnection connection, DbContextOptions<AppDbContext> options, List<Guid> establishments)
        {
            this.connection = connection;
            this.options = options;
            Establishments = establishments;
            Services = null!;
        }

        public ServiceProvider Services { get; private set; }

        /// <summary>Tenants the hosted service established a trusted context for.</summary>
        public List<Guid> Establishments { get; }

        public static async Task<DeliveryHarness> CreateAsync(RecordingPushHandler handler)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction("now", () => DateTime.UtcNow);
            connection.CreateFunction("uuid_generate_v4", () => Guid.NewGuid());

            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var configuration = VapidConfiguration();
            var harness = new DeliveryHarness(connection, options, []);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
            services.AddSingleton<IConfiguration>(configuration);
            services.AddScoped<ScopeTenantContext>();
            services.AddScoped<IEffectiveTenantContextAccessor>(sp => sp.GetRequiredService<ScopeTenantContext>());
            services.AddScoped<ICrossTenantWriteAuthorizationAccessor>(sp => sp.GetRequiredService<ScopeTenantContext>());
            services.AddScoped(sp => new AppDbContext(
                options,
                new Microsoft.AspNetCore.Http.HttpContextAccessor(),
                configuration,
                sp.GetRequiredService<IEffectiveTenantContextAccessor>(),
                sp.GetRequiredService<ICrossTenantWriteAuthorizationAccessor>()));
            services.AddScoped<WebPushVapidProvider>();
            services.AddSingleton(new WebPushSender(new StubHttpClientFactory(handler), NullLogger<WebPushSender>.Instance));
            services.AddScoped<ITrustedInvocationContextAccessor>(sp =>
                new TestInvocationContextAccessor(sp.GetRequiredService<ScopeTenantContext>().EffectiveTenantId ?? Guid.Empty));
            services.AddScoped<NotificationPushService>();
            services.AddScoped<ISmsGatewayServiceWrapper, UnusedSmsGateway>();
            services.AddScoped<NotificationDeliveryDispatcher>();
            services.AddSingleton<NotificationDeliverySignal>();
            services.AddScoped<ITrustedServiceTargetContextInitializer>(sp =>
                new RecordingContextInitializer(sp.GetRequiredService<ScopeTenantContext>(), harness.Establishments));

            harness.Services = services.BuildServiceProvider();

            await using (var seed = harness.CreateContext(Guid.NewGuid()))
                await seed.Database.EnsureCreatedAsync();

            return harness;
        }

        public AppDbContext CreateContext(Guid? tenantId)
        {
            var context = new ScopeTenantContext();
            context.Establish(tenantId);
            return new AppDbContext(
                options,
                new Microsoft.AspNetCore.Http.HttpContextAccessor(),
                VapidConfiguration(),
                context,
                context);
        }

        /// <summary>Registers a device and queues one routing-only message push for that tenant.</summary>
        public async Task<Guid> QueueMessagePushAsync(Guid tenantId)
        {
            var credentialId = Guid.NewGuid();
            await using var db = CreateContext(tenantId);
            var invocation = new TestInvocationContextAccessor(tenantId, credentialId);
            var push = NotificationTestHost.CreatePushService(db, invocation, VapidConfiguration(), new UnusedHandler());

            await push.RegisterAsync(new RegisterPushSubscriptionRequest
            {
                CredentialId = credentialId,
                Endpoint = $"https://push.example.net/{Guid.NewGuid():N}",
                P256dh = P256dh,
                Auth = Auth
            }, CancellationToken.None);

            var created = await NotificationTestHost.CreateNotificationService(db, invocation, push)
                .CreateNotificationAsync(new CreateNotificationRequest
                {
                    TenantId = tenantId,
                    RecipientCredentialId = credentialId,
                    TemplateKey = NotificationTemplateKeys.MessageReceived,
                    Title = "New message",
                    Body = "ciphertext the server cannot read",
                    DeliveryChannels = NotificationDeliveryChannel.Push,
                    Data = new Dictionary<string, string> { ["ThreadId"] = Guid.NewGuid().ToString() }
                }, CancellationToken.None);
            created.IsSuccess.Should().BeTrue();

            return await db.Set<NotificationDeliveryJob>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Channel == NotificationDeliveryChannel.Push)
                .Select(x => x.Id)
                .SingleAsync();
        }

        public async Task<NotificationDeliveryJob> ReadJobAsync(Guid jobId)
        {
            await using var db = CreateContext(null);
            return await db.Set<NotificationDeliveryJob>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == jobId);
        }

        /// <summary>Runs the real hosted service until <paramref name="until"/> holds or time runs out.</summary>
        public async Task RunHostedServiceAsync(Func<bool> until)
        {
            var host = new NotificationDeliveryDispatcherHostedService(
                Services.GetRequiredService<IServiceScopeFactory>(),
                Services.GetRequiredService<NotificationDeliverySignal>(),
                NullLogger<NotificationDeliveryDispatcherHostedService>.Instance,
                VapidConfiguration());

            await host.StartAsync(CancellationToken.None);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!until() && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            await host.StopAsync(CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private static IConfiguration VapidConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Push:Vapid:PublicKey"] = VapidPublic,
                ["Notifications:Push:Vapid:PrivateKey"] = VapidPrivate,
                ["Notifications:Push:Vapid:Subject"] = "mailto:ops@example.net",
                ["Notifications:Delivery:PollSeconds"] = "5"
            })
            .Build();

    /// <summary>A scope's tenant context: untrusted until something establishes it, as in production.</summary>
    private sealed class ScopeTenantContext : IEffectiveTenantContextAccessor, ICrossTenantWriteAuthorizationAccessor
    {
        public bool HasTrustedInvocation { get; private set; }
        public Guid? EffectiveTenantId { get; private set; }
        public bool IsAuthorized => true;

        public void Establish(Guid? tenantId)
        {
            HasTrustedInvocation = true;
            EffectiveTenantId = tenantId;
        }
    }

    /// <summary>Stands in for IdentityServer: records the tenant and establishes the scope's context.</summary>
    private sealed class RecordingContextInitializer(ScopeTenantContext context, List<Guid> establishments)
        : ITrustedServiceTargetContextInitializer
    {
        public Task<TrustedInvocationResult> EstablishAsync(
            Guid targetTenantId,
            string audience,
            IReadOnlyCollection<string> requiredServiceScopes,
            string allowedServiceCaller,
            Guid? correlationId = null,
            CancellationToken ct = default)
        {
            context.Establish(targetTenantId);
            lock (establishments)
                establishments.Add(targetTenantId);
            return Task.FromResult(Granted(targetTenantId));
        }

        public Task<TrustedInvocationResult> EstablishTenantlessAsync(
            string audience,
            IReadOnlyCollection<string> requiredServiceScopes,
            string allowedServiceCaller,
            Guid? correlationId = null,
            CancellationToken ct = default)
        {
            context.Establish(null);
            return Task.FromResult(Granted(null));
        }

        private static TrustedInvocationResult Granted(Guid? tenantId) =>
            TrustedInvocationResult.Success(new TrustedInvocationContext(null, null, tenantId, null, Guid.NewGuid()));
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class UnusedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Queueing must not reach a push service.");
    }

    private sealed class UnusedSmsGateway : ISmsGatewayServiceWrapper
    {
        public Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request) => throw new NotSupportedException();
        public Task<QueryResponse<List<SmsNodeJob>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request) => throw new NotSupportedException();
        public Task<QueryResponse<List<SmsNodeJob>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request) => throw new NotSupportedException();
    }
}
