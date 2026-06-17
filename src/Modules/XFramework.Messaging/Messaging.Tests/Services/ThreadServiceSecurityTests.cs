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
