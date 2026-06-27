using System.Net;
using System.Text.Json;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Api.Services;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using Messaging.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;

namespace Messaging.Tests.Services;

public sealed class ThreadServiceSecurityTests
{
    [Test]
    public async Task CreateThreadMessageAsync_SpoofedSenderCredential_UsesAuthenticatedCaller()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var callerCredentialId = Guid.NewGuid();
        var spoofedCredentialId = Guid.NewGuid();
        var callerMemberId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Member(callerMemberId, threadId, callerCredentialId, tenantId),
            Member(Guid.NewGuid(), threadId, spoofedCredentialId, tenantId));

        var service = CreateService(dataContext);
        var request = new CreateThreadMessageRequest
        {
            ThreadId = threadId,
            SenderCredentialId = spoofedCredentialId,
            Text = "spoof attempt",
            Metadata = Metadata(callerCredentialId, tenantId)
        };

        var result = await service.CreateThreadMessageAsync(request);

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var message = dataContext.Set<Message>().Single();
        Assert.That(message.MessageThreadMemberId, Is.EqualTo(callerMemberId));
        Assert.That(dataContext.Set<MessageOutboxEvent>().Single().ActorCredentialId, Is.EqualTo(callerCredentialId));
    }

    [Test]
    public async Task CreateThreadMessageAsync_CrossTenantThreadId_ReturnsNotFound()
    {
        var threadTenantId = Guid.NewGuid();
        var callerTenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var callerCredentialId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, threadTenantId),
            Member(Guid.NewGuid(), threadId, callerCredentialId, threadTenantId));

        var service = CreateService(dataContext);
        var request = new CreateThreadMessageRequest
        {
            ThreadId = threadId,
            SenderCredentialId = callerCredentialId,
            Text = "cross tenant attempt",
            Metadata = Metadata(callerCredentialId, callerTenantId)
        };

        var result = await service.CreateThreadMessageAsync(request);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(404));
        Assert.That(dataContext.Set<Message>(), Is.Empty);
        Assert.That(dataContext.Set<MessageOutboxEvent>(), Is.Empty);
    }

    [Test]
    public async Task AddThreadMemberAsync_ExplicitRolesWithoutAdmin_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var actorCredentialId = Guid.NewGuid();
        var targetCredentialId = Guid.NewGuid();
        var actorMemberId = Guid.NewGuid();
        var userRoleId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Group(Guid.NewGuid(), threadId, tenantId),
            Member(actorMemberId, threadId, actorCredentialId, tenantId),
            Credential(targetCredentialId, tenantId),
            IdentityRole(userRoleId, actorCredentialId, Guid.NewGuid(), tenantId),
            ThreadRole(Guid.NewGuid(), actorMemberId, userRoleId, tenantId));

        var service = CreateService(dataContext);
        var request = new AddThreadMemberRequest
        {
            ThreadId = threadId,
            CredentialId = targetCredentialId,
            Metadata = Metadata(actorCredentialId, tenantId)
        };

        var result = await service.AddThreadMemberAsync(request);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
        Assert.That(dataContext.Set<MessageThreadMember>().Any(m => m.CredentialId == targetCredentialId), Is.False);
        Assert.That(dataContext.Set<MessageOutboxEvent>(), Is.Empty);
    }

    [Test]
    public async Task AddThreadMemberAsync_AdminRole_AddsMember()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var actorCredentialId = Guid.NewGuid();
        var targetCredentialId = Guid.NewGuid();
        var actorMemberId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Group(Guid.NewGuid(), threadId, tenantId),
            Member(actorMemberId, threadId, actorCredentialId, tenantId),
            Credential(targetCredentialId, tenantId),
            IdentityRole(adminRoleId, actorCredentialId, IdentityConstants.RoleType.Admin, tenantId),
            ThreadRole(Guid.NewGuid(), actorMemberId, adminRoleId, tenantId));

        var service = CreateService(dataContext);
        var request = new AddThreadMemberRequest
        {
            ThreadId = threadId,
            CredentialId = targetCredentialId,
            Metadata = Metadata(actorCredentialId, tenantId)
        };

        var result = await service.AddThreadMemberAsync(request);

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(dataContext.Set<MessageThreadMember>().Any(m => m.CredentialId == targetCredentialId), Is.True);
        Assert.That(dataContext.Set<MessageOutboxEvent>().Single().EventType, Is.EqualTo(MessageRealtimeEvents.ThreadMemberAdded));
    }

    [Test]
    public async Task CreateDirectThreadAsync_ExistingPair_ReturnsExistingThreadWithoutDuplicate()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var callerCredentialId = Guid.NewGuid();
        var otherCredentialId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Member(Guid.NewGuid(), threadId, callerCredentialId, tenantId),
            Member(Guid.NewGuid(), threadId, otherCredentialId, tenantId),
            Credential(callerCredentialId, tenantId),
            Credential(otherCredentialId, tenantId));

        var service = CreateService(dataContext);
        var result = await service.CreateDirectThreadAsync(new CreateDirectThreadRequest
        {
            OtherCredentialId = otherCredentialId,
            Metadata = Metadata(callerCredentialId, tenantId)
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.ThreadId, Is.EqualTo(threadId));
        Assert.That(dataContext.Set<MessageThread>().Count, Is.EqualTo(1));
        Assert.That(dataContext.Set<MessageOutboxEvent>(), Is.Empty);
    }

    [Test]
    public async Task GetUnreadCountsAsync_CountsOnlyUnreadMessagesFromOtherMembers()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var callerCredentialId = Guid.NewGuid();
        var otherCredentialId = Guid.NewGuid();
        var callerMemberId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var readMessageId = Guid.NewGuid();
        var unreadMessageId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Member(callerMemberId, threadId, callerCredentialId, tenantId),
            Member(otherMemberId, threadId, otherCredentialId, tenantId),
            Message(readMessageId, threadId, otherMemberId, tenantId, "read"),
            Message(unreadMessageId, threadId, otherMemberId, tenantId, "unread"),
            Message(Guid.NewGuid(), threadId, callerMemberId, tenantId, "own"),
            Delivery(Guid.NewGuid(), callerMemberId, readMessageId, tenantId, MessageDeliveryTypes.Read));

        var service = CreateService(dataContext);
        var result = await service.GetUnreadCountsAsync(new GetUnreadCountsRequest
        {
            Metadata = Metadata(callerCredentialId, tenantId)
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TotalUnreadCount, Is.EqualTo(1));
        Assert.That(result.Data.Threads.Single().UnreadCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateThreadMessageAsync_BlockedDirectThread_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var callerCredentialId = Guid.NewGuid();
        var otherCredentialId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Member(Guid.NewGuid(), threadId, callerCredentialId, tenantId),
            Member(Guid.NewGuid(), threadId, otherCredentialId, tenantId),
            Block(Guid.NewGuid(), callerCredentialId, otherCredentialId, tenantId));

        var service = CreateService(dataContext);
        var result = await service.CreateThreadMessageAsync(new CreateThreadMessageRequest
        {
            ThreadId = threadId,
            Text = "blocked",
            Metadata = Metadata(callerCredentialId, tenantId)
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
        Assert.That(dataContext.Set<Message>(), Is.Empty);
        Assert.That(dataContext.Set<MessageOutboxEvent>(), Is.Empty);
    }

    [Test]
    public async Task CreateThreadMessageAsync_WithReplyAndMentions_PersistsMetadata()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var callerCredentialId = Guid.NewGuid();
        var mentionedCredentialId = Guid.NewGuid();
        var callerMemberId = Guid.NewGuid();
        var mentionedMemberId = Guid.NewGuid();
        var parentMessageId = Guid.NewGuid();

        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Member(callerMemberId, threadId, callerCredentialId, tenantId),
            Member(mentionedMemberId, threadId, mentionedCredentialId, tenantId),
            Message(parentMessageId, threadId, mentionedMemberId, tenantId, "parent"));

        var service = CreateService(dataContext);
        var result = await service.CreateThreadMessageAsync(new CreateThreadMessageRequest
        {
            ThreadId = threadId,
            Text = "reply",
            ParentMessageId = parentMessageId,
            MentionedCredentialIds = [mentionedCredentialId],
            Metadata = Metadata(callerCredentialId, tenantId)
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var createdMessage = dataContext.Set<Message>().Single(message => message.Id == result.Data!.MessageId);
        Assert.That(createdMessage.ParentMessageId, Is.EqualTo(parentMessageId));

        var mentions = JsonSerializer.Deserialize<List<Guid>>(createdMessage.MentionedCredentialIdsJson);
        Assert.That(mentions, Is.EquivalentTo(new[] { mentionedCredentialId }));
    }

    [Test]
    public async Task CreateThreadMessageAsync_WithTemplate_StoresRenderedTextAndTemplateAuditFields()
    {
        var tenantId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var callerCredentialId = Guid.NewGuid();
        var callerMemberId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            Thread(threadId, tenantId),
            Member(callerMemberId, threadId, callerCredentialId, tenantId));
        var service = CreateService(
            dataContext,
            new TestMessagingTemplateService(new RenderMessageTemplateResponse
            {
                TemplateId = templateId,
                TemplateKey = "tenant.notice",
                TemplateType = MessageTemplateTypes.Tenant,
                Body = "Rendered notice",
                TemplateVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = "Ava"
                }
            }));

        var result = await service.CreateThreadMessageAsync(new CreateThreadMessageRequest
        {
            ThreadId = threadId,
            TemplateId = templateId,
            TemplateVariables = new Dictionary<string, string> { ["Name"] = "Ava" },
            Metadata = Metadata(callerCredentialId, tenantId)
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var message = dataContext.Set<Message>().Single();
        Assert.That(message.Text, Is.EqualTo("Rendered notice"));
        Assert.That(message.TemplateId, Is.EqualTo(templateId));
        Assert.That(message.TemplateKey, Is.EqualTo("tenant.notice"));
        Assert.That(message.TemplateType, Is.EqualTo(MessageTemplateTypes.Tenant));
        Assert.That(message.TemplateVariablesJson, Does.Contain("Ava"));
    }

    private static ThreadService CreateService(
        InMemoryDataContext dataContext,
        IMessagingTemplateService? templateService = null) =>
        new(
            dataContext,
            new MessagingRequestContextResolver(new HttpContextAccessor()),
            templateService ?? new TestMessagingTemplateService(),
            new TestStorageServiceWrapper(),
            NullLogger<ThreadService>.Instance);

    private sealed class TestStorageServiceWrapper : IStorageServiceWrapper
    {
        public IStorageFileCrudService StorageFile { get; init; } = null!;
        public IStorageFileTypeCrudService StorageFileType { get; init; } = null!;

        public Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default) =>
            throw new NotSupportedException("Storage queries are not used by these tests.");

        public Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default) =>
            throw new NotSupportedException("Storage changes are not used by these tests.");

        public IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] queryDescriptorBytes, CancellationToken ct = default) =>
            throw new NotSupportedException("Storage query streams are not used by these tests.");

        public Task<QueryResponse<StorageUploadSessionResponse>> CreateStorageUploadSession(CreateStorageUploadSessionRequest request) =>
            throw new NotSupportedException("Storage upload sessions are not used by these tests.");

        public Task<QueryResponse<StorageUploadPartResponse>> UploadStorageFilePart(UploadStorageFilePartRequest request) =>
            throw new NotSupportedException("Storage upload parts are not used by these tests.");

        public Task<QueryResponse<StorageUploadPartListResponse>> ListStorageUploadParts(ListStorageUploadPartsRequest request) =>
            throw new NotSupportedException("Storage upload parts are not used by these tests.");

        public Task<QueryResponse<StorageFileResponse>> CompleteStorageUploadSession(CompleteStorageUploadSessionRequest request) =>
            throw new NotSupportedException("Storage upload completion is not used by these tests.");

        public Task<CmdResponse> AbortStorageUploadSession(AbortStorageUploadSessionRequest request) =>
            throw new NotSupportedException("Storage upload abort is not used by these tests.");

        public Task<QueryResponse<StorageFileResponse>> GetStorageFile(GetStorageFileRequest request) =>
            throw new NotSupportedException("Storage file reads are not used by these tests.");

        public Task<QueryResponse<StorageFileListResponse>> GetStorageFiles(GetStorageFilesRequest request) =>
            throw new NotSupportedException("Storage file listing is not used by these tests.");

        public Task<QueryResponse<StorageDownloadUrlResponse>> GetStorageDownloadUrl(GetStorageDownloadUrlRequest request) =>
            throw new NotSupportedException("Storage download URLs are not used by these tests.");

        public Task<QueryResponse<StoragePublicUrlResponse>> GetStoragePublicUrl(GetStoragePublicUrlRequest request) =>
            throw new NotSupportedException("Storage public URLs are not used by these tests.");

        public Task<CmdResponse> DeleteStorageFile(DeleteStorageFileRequest request) =>
            throw new NotSupportedException("Storage deletes are not used by these tests.");

        public Task<QueryResponse<StorageFileResponse>> RestoreStorageFile(RestoreStorageFileRequest request) =>
            throw new NotSupportedException("Storage restores are not used by these tests.");

        public Task<QueryResponse<StorageRetentionCleanupResponse>> CleanupStorageRetention(CleanupStorageRetentionRequest request) =>
            throw new NotSupportedException("Storage cleanup is not used by these tests.");

        public Task<QueryResponse<StorageFileValidationResponse>> ValidateStorageFileReference(ValidateStorageFileReferenceRequest request) =>
            Task.FromResult(new QueryResponse<StorageFileValidationResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new StorageFileValidationResponse
                {
                    StorageFileId = request.StorageFileId,
                    TenantId = request.Metadata?.TenantId ?? Guid.Empty,
                    IsValid = true,
                    Status = StorageFileStatus.Available,
                    Visibility = StorageFileVisibility.Private,
                    Name = "attachment.png",
                    ContentType = "image/png",
                    ContentLengthBytes = 1024
                }
            });
    }

    private static RequestMetadata Metadata(Guid credentialId, Guid tenantId) => new()
    {
        CredentialId = credentialId,
        TenantId = tenantId
    };

    private static MessageThread Thread(Guid id, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        TypeId = Guid.NewGuid(),
        Name = "Thread",
        Description = string.Empty,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageThreadMemberGroup Group(Guid id, Guid threadId, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageThreadId = threadId,
        Alias = "Default",
        Emoji = string.Empty,
        Description = string.Empty,
        Status = 1,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageThreadMember Member(Guid id, Guid threadId, Guid credentialId, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageThreadId = threadId,
        CredentialId = credentialId,
        GroupId = Guid.NewGuid(),
        Alias = string.Empty,
        Emoji = string.Empty,
        Description = string.Empty,
        Status = 1,
        Role = MessageThreadMemberRoles.Member,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static Message Message(Guid id, Guid threadId, Guid memberId, Guid tenantId, string text) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageThreadId = threadId,
        MessageThreadMemberId = memberId,
        Text = text,
        MentionedCredentialIdsJson = "[]",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageDelivery Delivery(Guid id, Guid memberId, Guid messageId, Guid tenantId, Guid typeId) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageThreadMemberId = memberId,
        MessageId = messageId,
        TypeId = typeId,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageBlock Block(Guid id, Guid blockerCredentialId, Guid blockedCredentialId, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        BlockerCredentialId = blockerCredentialId,
        BlockedCredentialId = blockedCredentialId,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static IdentityCredential Credential(Guid id, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        UserName = $"user-{id:N}",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static IdentityRole IdentityRole(Guid id, Guid credentialId, Guid roleTypeId, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        CredentialId = credentialId,
        TypeId = roleTypeId,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageThreadMemberRole ThreadRole(
        Guid id,
        Guid memberId,
        Guid roleId,
        Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageThreadMemberId = memberId,
        RoleId = roleId,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private sealed class TestMessagingTemplateService(
        RenderMessageTemplateResponse? renderResponse = null) : IMessagingTemplateService
    {
        public Task<Result<GetMessageTemplatesResponse>> GetTemplatesAsync(
            GetMessageTemplatesRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(Result<GetMessageTemplatesResponse>.Failure("Template service is not configured for this test.", 501));

        public Task<Result<MessageTemplateResponse>> GetTemplateAsync(
            GetMessageTemplateRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(Result<MessageTemplateResponse>.Failure("Template service is not configured for this test.", 501));

        public Task<Result<MessageTemplateResponse>> CreateTemplateAsync(
            CreateMessageTemplateRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(Result<MessageTemplateResponse>.Failure("Template service is not configured for this test.", 501));

        public Task<Result<MessageTemplateResponse>> UpdateTemplateAsync(
            UpdateMessageTemplateRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(Result<MessageTemplateResponse>.Failure("Template service is not configured for this test.", 501));

        public Task<Result<CmdResponse>> DeleteTemplateAsync(
            DeleteMessageTemplateRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(Result<CmdResponse>.Failure("Template service is not configured for this test.", 501));

        public Task<Result<MessageTemplateResponse>> CloneTemplateAsync(
            CloneMessageTemplateRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(Result<MessageTemplateResponse>.Failure("Template service is not configured for this test.", 501));

        public Task<Result<RenderMessageTemplateResponse>> RenderTemplateAsync(
            RenderMessageTemplateRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(renderResponse is null
                ? Result<RenderMessageTemplateResponse>.Failure("Template service is not configured for this test.", 501)
                : Result<RenderMessageTemplateResponse>.Success(renderResponse));
    }
}
