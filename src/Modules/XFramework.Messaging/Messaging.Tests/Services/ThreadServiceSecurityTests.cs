using System.Text.Json;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Api.Services;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;

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

    private static ThreadService CreateService(InMemoryDataContext dataContext) =>
        new(
            dataContext,
            new MessagingRequestContextResolver(new HttpContextAccessor()),
            NullLogger<ThreadService>.Instance);

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
}
