using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Api.Services;
using Notifications.Api.Services.Push;
using Notifications.Domain.Shared.Contracts;
using XFramework.Domain.Contexts;
using XFramework.Integration.Security;

namespace Notifications.Tests.Services;

/// <summary>Shared SQLite-backed harness so notification and push tests build the same object graph.</summary>
internal sealed class NotificationTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private NotificationTestDatabase(SqliteConnection connection, AppDbContext context, Guid tenantId)
    {
        this.connection = connection;
        Context = context;
        TenantId = tenantId;
    }

    public AppDbContext Context { get; }
    public Guid TenantId { get; }

    public static async Task<NotificationTestDatabase> CreateAsync()
    {
        var tenantId = Guid.NewGuid();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction("now", () => DateTime.UtcNow);
        connection.CreateFunction("uuid_generate_v4", () => Guid.NewGuid());

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = tenantId.ToString()
            })
            .Build();

        var context = new AppDbContext(
            options,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            configuration,
            new TestEffectiveTenantContextAccessor(tenantId),
            new TestCrossTenantWriteAuthorizationAccessor());

        _ = typeof(NotificationInboxItem).Assembly;
        await context.Database.EnsureCreatedAsync();

        return new NotificationTestDatabase(connection, context, tenantId);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }

    private sealed class TestEffectiveTenantContextAccessor(Guid tenantId)
        : XFramework.Domain.Shared.Security.IEffectiveTenantContextAccessor
    {
        public bool HasTrustedInvocation => true;
        public Guid? EffectiveTenantId => tenantId;
    }

    private sealed class TestCrossTenantWriteAuthorizationAccessor
        : XFramework.Domain.Shared.Security.ICrossTenantWriteAuthorizationAccessor
    {
        public bool IsAuthorized => true;
    }
}

internal sealed class TestInvocationContextAccessor(Guid tenantId, Guid? credentialId = null)
    : ITrustedInvocationContextAccessor
{
    public TrustedInvocationContext Current { get; } =
        new(
            new TrustedActorIdentity(
                credentialId ?? Guid.NewGuid(),
                Guid.NewGuid(),
                tenantId,
                Guid.NewGuid(),
                new HashSet<string>(StringComparer.Ordinal) { "notifications-test" },
                new HashSet<string>(StringComparer.Ordinal),
                "notifications-tests-g1",
                DateTimeOffset.UtcNow.AddHours(1)),
            null,
            tenantId,
            null,
            Guid.NewGuid());
}

internal static class NotificationTestHost
{
    /// <summary>Builds the push service. With no VAPID configuration it reports push as unavailable.</summary>
    public static NotificationPushService CreatePushService(
        AppDbContext db,
        ITrustedInvocationContextAccessor invocation,
        IConfiguration? configuration = null,
        HttpMessageHandler? handler = null) =>
        new(
            db,
            new WebPushVapidProvider(
                db,
                configuration ?? new ConfigurationBuilder().Build(),
                NullLogger<WebPushVapidProvider>.Instance),
            new WebPushSender(
                new StubHttpClientFactory(handler ?? new UnusedHandler()),
                NullLogger<WebPushSender>.Instance),
            NullLogger<NotificationPushService>.Instance,
            invocation);

    public static NotificationService CreateNotificationService(
        AppDbContext db,
        ITrustedInvocationContextAccessor invocation,
        NotificationPushService? push = null) =>
        new(
            db,
            NullLogger<NotificationService>.Instance,
            invocation,
            push ?? CreatePushService(db, invocation),
            new NotificationDeliverySignal());

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class UnusedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This test must not reach a push service.");
    }
}

/// <summary>Records every push request and replies with a scripted status per endpoint host.</summary>
internal sealed class RecordingPushHandler(Func<HttpRequestMessage, System.Net.HttpStatusCode> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<byte[]> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken));
        return new HttpResponseMessage(respond(request));
    }
}
