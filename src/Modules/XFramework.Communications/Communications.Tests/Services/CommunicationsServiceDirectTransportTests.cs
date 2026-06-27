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
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using Notifications.Integration.Drivers;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;

namespace Communications.Tests.Services;

public sealed class CommunicationsServiceDirectTransportTests
{
    private const string TrustedMetadataSecret = "communications-direct-transport-test-secret";

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
            new CommunicationsRequestContextResolver(new HttpContextAccessor(), TestConfiguration()),
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
            new CommunicationsRequestContextResolver(new HttpContextAccessor(), TestConfiguration()),
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
            new CommunicationsRequestContextResolver(new HttpContextAccessor(), TestConfiguration()),
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
            new CommunicationsRequestContextResolver(new HttpContextAccessor(), TestConfiguration()),
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

    private static RequestMetadata Metadata(Guid tenantId)
    {
        var metadata = new RequestMetadata { TenantId = tenantId, Name = "ControlPanel" };
        RequestMetadataTrust.Sign(metadata, TrustedMetadataSecret);
        return metadata;
    }

    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Communications:TrustedMetadata:SharedSecret"] = TrustedMetadataSecret
            })
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
        bool throwOnCreate = false) : INotificationsServiceWrapper
    {
        public Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(
            CreateNotificationRequest request,
            CancellationToken ct = default) =>
            throwOnCreate
                ? throw new InvalidOperationException("Notifications unavailable")
                :
            response is null
                ? throw new AssertionException("Notifications should not be called for unsupported transports.")
                : Task.FromResult(response);

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
}
