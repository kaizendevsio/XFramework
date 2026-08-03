using IdentityServer.Domain.Shared.Contracts;
using Communications.Api.Services;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Create;
using Communications.Domain.Shared.Contracts.Requests.Templates;
using Communications.Domain.Shared.Contracts.Requests.Update;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using Notifications.Integration.Drivers;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;

namespace Communications.Tests.Services;

public sealed class CommunicationsServiceDirectTransportTests
{
    [Test]
    public async Task CreateDirectMessageAsync_UnsupportedTransport_DoesNotPersistDirectMessage()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            new TestNotificationsServiceWrapper(),
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);

        var result = await service.CreateDirectMessageAsync(new CreateDirectMessageRequest
        {
            Metadata = Metadata(tenantId),
            MessageTransportType = MessageTransportType.Push,
            Recipient = "person@example.test",
            Subject = "Subject",
            Message = "Message"
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(501));
        Assert.That(dataContext.Set<MessageDirect>(), Is.Empty);
    }

    [Test]
    public async Task CreateDirectMessageAsync_WhenNotificationsFails_MarksDirectMessageFailed()
    {
        var tenantId = Guid.NewGuid();
        var agentClusterId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            new TestNotificationsServiceWrapper(new QueryResponse<NotificationInboxItemResponse>
            {
                HttpStatusCode = System.Net.HttpStatusCode.BadGateway,
                Message = "Gateway unavailable"
            }),
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);

        var result = await service.CreateDirectMessageAsync(new CreateDirectMessageRequest
        {
            Metadata = Metadata(tenantId),
            MessageTransportType = MessageTransportType.Sms,
            AgentClusterId = agentClusterId,
            Recipient = "+15555550123",
            Message = "Message"
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(502));
        var record = dataContext.Set<MessageDirect>().Single();
        Assert.That(record.Status, Is.EqualTo(MessageStatus.Failed));
        Assert.That(record.MessageTransportType, Is.EqualTo(MessageTransportType.Sms));
    }

    [Test]
    public async Task CreateDirectMessageAsync_WhenMetadataIsNotTrusted_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            new TestNotificationsServiceWrapper(),
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);

        var result = await service.CreateDirectMessageAsync(new CreateDirectMessageRequest
        {
            Metadata = new RequestMetadata { TenantId = tenantId },
            MessageTransportType = MessageTransportType.Sms,
            AgentClusterId = Guid.NewGuid(),
            Recipient = "+15555550123",
            Message = "Message"
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
        Assert.That(dataContext.Set<MessageDirect>(), Is.Empty);
    }

    [Test]
    public async Task CreateDirectMessageAsync_WhenNotificationsThrows_MarksDirectMessageFailed()
    {
        var tenantId = Guid.NewGuid();
        var agentClusterId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            new TestNotificationsServiceWrapper(throwOnCreate: true),
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);

        var result = await service.CreateDirectMessageAsync(new CreateDirectMessageRequest
        {
            Metadata = Metadata(tenantId),
            MessageTransportType = MessageTransportType.Sms,
            AgentClusterId = agentClusterId,
            Recipient = "+15555550123",
            Message = "Message"
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(502));
        Assert.That(dataContext.Set<MessageDirect>().Single().Status, Is.EqualTo(MessageStatus.Failed));
    }

    [TestCase("person@example.test")]
    [TestCase("+15555550123")]
    public async Task CreateDirectMessageAsync_WhenDeliveryFails_DoesNotLogRawRecipient(string recipient)
    {
        var tenantId = Guid.NewGuid();
        var logger = new CapturingLogger<CommunicationsService>();
        var dataContext = new InMemoryDataContext();
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            new TestNotificationsServiceWrapper(throwOnCreate: true, exceptionMessage: recipient),
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            logger);

        await service.CreateDirectMessageAsync(new CreateDirectMessageRequest
        {
            Metadata = Metadata(tenantId),
            MessageTransportType = recipient.Contains('@')
                ? MessageTransportType.Email
                : MessageTransportType.Sms,
            AgentClusterId = Guid.NewGuid(),
            Recipient = recipient,
            Message = "Message"
        });

        Assert.That(logger.Messages, Is.Not.Empty);
        Assert.That(logger.Messages, Has.None.Contains(recipient));
    }

    [Test]
    public async Task CreateDirectMessageAsync_ConcurrentSameRequestId_PersistsAndNotifiesOnce()
    {
        var tenantId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dataContext = new ConcurrentIdempotencyDataContext();
        var notifications = new TestNotificationsServiceWrapper(
            new QueryResponse<NotificationInboxItemResponse>
            {
                HttpStatusCode = System.Net.HttpStatusCode.Accepted,
                Response = new NotificationInboxItemResponse()
            });
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            notifications,
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);

        CreateDirectMessageRequest Request() => new()
        {
            Metadata = Metadata(tenantId, requestId),
            MessageTransportType = MessageTransportType.Email,
            AgentClusterId = Guid.NewGuid(),
            Recipient = "person@example.test",
            Message = "Message"
        };

        var results = await Task.WhenAll(
            service.CreateDirectMessageAsync(Request()),
            service.CreateDirectMessageAsync(Request()));

        Assert.That(
            results.Count(result => result.IsSuccess),
            Is.EqualTo(1),
            string.Join(" | ", results.Select(result => $"{result.StatusCode}:{result.Message}")));
        Assert.That(results.Count(result => !result.IsSuccess && result.StatusCode == 409), Is.EqualTo(1));
        Assert.That(dataContext.Set<MessageDirect>(), Has.Count.EqualTo(1));
        Assert.That(dataContext.Set<MessageDirect>().Single().IdempotencyRequestId, Is.EqualTo(requestId));
        Assert.That(notifications.CreateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateDirectMessageAsync_SensitiveContent_IsNeverPersistedInCommunications()
    {
        var tenantId = Guid.NewGuid();
        var token = $"reset-{Guid.NewGuid():N}";
        var recipient = "person@example.test";
        var dataContext = new InMemoryDataContext();
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            SuccessfulNotifications(),
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);

        var result = await service.CreateDirectMessageAsync(new CreateDirectMessageRequest
        {
            Metadata = Metadata(tenantId),
            MessageTransportType = MessageTransportType.Email,
            Recipient = recipient,
            Subject = token,
            Message = token
        });

        Assert.That(result.IsSuccess, Is.True);
        var record = dataContext.Set<MessageDirect>().Single();
        Assert.That(record.Status, Is.EqualTo(MessageStatus.Sent));
        Assert.That(record.Message, Is.EqualTo("[redacted]"));
        Assert.That(record.ExternalRecipient, Is.Null);
        Assert.That(record.Subject, Is.Null);
        Assert.That(record.TemplateVariablesJson, Is.EqualTo("{}"));
    }

    [Test]
    public async Task CreateDirectMessageAsync_FailedRequestId_IsRetriedAndFinalized()
    {
        var tenantId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var notifications = new SequencedNotificationsServiceWrapper(
            FailureNotification("sensitive-provider-detail"),
            SuccessNotification());
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            notifications,
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);
        var request = new CreateDirectMessageRequest
        {
            Metadata = Metadata(tenantId, requestId),
            MessageTransportType = MessageTransportType.Sms,
            Recipient = "+15555550123",
            Message = "One-time code"
        };

        var first = await service.CreateDirectMessageAsync(request);
        var second = await service.CreateDirectMessageAsync(request);

        Assert.That(first.IsSuccess, Is.False);
        Assert.That(first.Message, Does.Not.Contain("sensitive-provider-detail"));
        Assert.That(second.IsSuccess, Is.True);
        Assert.That(notifications.CreateCount, Is.EqualTo(2));
        Assert.That(dataContext.Set<MessageDirect>(), Has.Count.EqualTo(1));
        Assert.That(dataContext.Set<MessageDirect>().Single().Status, Is.EqualTo(MessageStatus.Sent));
    }

    [Test]
    public async Task CreateDirectMessageAsync_ThrownSensitiveDetail_IsNotReturned()
    {
        var tenantId = Guid.NewGuid();
        var sensitiveDetail = "provider-secret-recipient@example.test";
        var dataContext = new InMemoryDataContext();
        var service = new CommunicationsService(
            dataContext,
            new TestTenantResolver(tenantId),
            new TestNotificationsServiceWrapper(throwOnCreate: true, exceptionMessage: sensitiveDetail),
            new TestCommunicationsTemplateService(),
            ContextResolver(),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())),
            new CommunicationsActionRateLimiter(),
            NullLogger<CommunicationsService>.Instance);

        var result = await service.CreateDirectMessageAsync(new CreateDirectMessageRequest
        {
            Metadata = Metadata(tenantId),
            MessageTransportType = MessageTransportType.Email,
            Recipient = "person@example.test",
            Message = "Message"
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Message, Does.Not.Contain(sensitiveDetail));
    }

    private static TestNotificationsServiceWrapper SuccessfulNotifications() =>
        new(SuccessNotification());

    private static QueryResponse<NotificationInboxItemResponse> SuccessNotification() => new()
    {
        HttpStatusCode = System.Net.HttpStatusCode.Accepted,
        Response = new NotificationInboxItemResponse()
    };

    private static QueryResponse<NotificationInboxItemResponse> FailureNotification(string message) => new()
    {
        HttpStatusCode = System.Net.HttpStatusCode.BadGateway,
        Message = message
    };

    private static RequestMetadata Metadata(Guid tenantId, Guid? requestId = null)
    {
        return new RequestMetadata
        {
            TenantId = tenantId,
            RequestId = requestId ?? Guid.NewGuid(),
            Name = "XFramework.Portal",
            ServiceAccessToken = FakeTrustedServiceInvocationResolver.ValidPortalToken
        };
    }

    private static CommunicationsRequestContextResolver ContextResolver() =>
        new(
            new HttpContextAccessor(),
            TestConfiguration(),
            serviceInvocationResolver: new FakeTrustedServiceInvocationResolver());

    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder()
            .Build();

    private sealed class TestTenantResolver(Guid tenantId) : ITenantResolver
    {
        public Task<Tenant> GetTenant(Guid? id)
        {
            if (id != tenantId)
                throw new InvalidOperationException("Tenant not found.");

            return Task.FromResult(new Tenant
            {
                Id = tenantId,
                TenantId = tenantId,
                Name = "Tenant",
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }
    }

    private sealed class TestNotificationsServiceWrapper(
        QueryResponse<NotificationInboxItemResponse>? response = null,
        bool throwOnCreate = false,
        string? exceptionMessage = null) : INotificationsServiceWrapper
    {
        private int createCount;

        public int CreateCount => Volatile.Read(ref createCount);

        public Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(
            CreateNotificationRequest request,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref createCount);
            return throwOnCreate
                ? throw new InvalidOperationException(exceptionMessage ?? "Notifications unavailable")
                :
            response is null
                ? throw new AssertionException("Notifications should not be called for unsupported transports.")
                : Task.FromResult(response);
        }

        public Task<QueryResponse<GetNotificationInboxResponse>> GetNotificationInbox(
            GetNotificationInboxRequest request,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CmdResponse> MarkNotificationRead(MarkNotificationReadRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<QueryResponse<NotificationPreferencesResponse>> UpdateNotificationPreferences(
            UpdateNotificationPreferencesRequest request,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<QueryResponse<NotificationDeliveryStatusResponse>> RecordNotificationDeliveryStatus(
            RecordNotificationDeliveryStatusRequest request,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestCommunicationsTemplateService : ICommunicationsTemplateService
    {
        public Task<Result<GetMessageTemplatesResponse>> GetTemplatesAsync(GetMessageTemplatesRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<MessageTemplateResponse>> GetTemplateAsync(GetMessageTemplateRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<MessageTemplateResponse>> CreateTemplateAsync(CreateMessageTemplateRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<MessageTemplateResponse>> UpdateTemplateAsync(UpdateMessageTemplateRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<CmdResponse>> DeleteTemplateAsync(DeleteMessageTemplateRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<MessageTemplateResponse>> CloneTemplateAsync(CloneMessageTemplateRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<RenderMessageTemplateResponse>> RenderTemplateAsync(RenderMessageTemplateRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class SequencedNotificationsServiceWrapper(
        params QueryResponse<NotificationInboxItemResponse>[] responses) : INotificationsServiceWrapper
    {
        private readonly Queue<QueryResponse<NotificationInboxItemResponse>> queue = new(responses);

        public int CreateCount { get; private set; }

        public Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(
            CreateNotificationRequest request,
            CancellationToken ct = default)
        {
            CreateCount++;
            return Task.FromResult(queue.Dequeue());
        }

        public Task<QueryResponse<GetNotificationInboxResponse>> GetNotificationInbox(
            GetNotificationInboxRequest request,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<CmdResponse> MarkNotificationRead(
            MarkNotificationReadRequest request,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<QueryResponse<NotificationPreferencesResponse>> UpdateNotificationPreferences(
            UpdateNotificationPreferencesRequest request,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<QueryResponse<NotificationDeliveryStatusResponse>> RecordNotificationDeliveryStatus(
            RecordNotificationDeliveryStatusRequest request,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class ConcurrentIdempotencyDataContext : IDataContext
    {
        private readonly object gate = new();
        private readonly List<object> persisted = [];
        private readonly AsyncLocal<List<object>?> pending = new();
        private readonly TaskCompletionSource bothInsertsReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int insertCount;

        public List<T> Set<T>() where T : class
        {
            lock (gate)
                return persisted.OfType<T>().ToList();
        }

        public IRemoteQuery<T> Query<T>() where T : class =>
            new InMemoryRemoteQuery<T>(Set<T>().AsQueryable());

        public void Add<T>(T entity) where T : class
        {
            (pending.Value ??= []).Add(entity);
            if (entity is MessageDirect && Interlocked.Increment(ref insertCount) == 2)
                bothInsertsReady.TrySetResult();
        }

        public void Update<T>(T entity) where T : class
        {
        }

        public void Remove<T>(T entity) where T : class
        {
            lock (gate)
                persisted.Remove(entity);
        }

        public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
        {
            var additions = pending.Value ?? [];
            pending.Value = null;
            var directMessage = additions.OfType<MessageDirect>().SingleOrDefault();
            if (directMessage is not null)
            {
                additions.Clear();
                await bothInsertsReady.Task.WaitAsync(ct);
                lock (gate)
                {
                    if (persisted.OfType<MessageDirect>().Any(message =>
                            message.TenantId == directMessage.TenantId &&
                            message.IdempotencyRequestId == directMessage.IdempotencyRequestId))
                    {
                        return DataContextResult.Failure("Duplicate direct-message request", 409);
                    }

                    persisted.Add(directMessage);
                }

                return DataContextResult.Success();
            }

            lock (gate)
                persisted.AddRange(additions);
            return DataContextResult.Success();
        }
    }
}
