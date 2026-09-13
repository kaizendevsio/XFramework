using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task ReactionRetry_AlreadyApplied_ReturnsSuccessWithoutAnotherWrite()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var member = Member(Guid.NewGuid(), Guid.NewGuid(), actor, tenant);
        var message = Message(Guid.NewGuid(), member.MessageThreadId, member.Id, tenant, "hello");
        var type = new MessageReactionType { Id = Guid.NewGuid(), TenantId = tenant, IsEnabled = true };
        var reaction = new MessageReaction { Id = Guid.NewGuid(), TenantId = tenant, MessageId = message.Id, MessageThreadMemberId = member.Id, TypeId = type.Id, IsEnabled = true };
        var context = new InMemoryDataContext(); context.Seed(Thread(member.MessageThreadId, tenant), member, message, type, reaction);
        var result = await CreateService(context).CreateMessageReactionAsync(new() { ThreadId = member.MessageThreadId, MessageId = message.Id, TypeId = type.Id, Metadata = Metadata(actor, tenant) });
        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(context.Set<MessageReaction>(), Has.Count.EqualTo(1));
        Assert.That(context.Set<MessageOutboxEvent>(), Is.Empty);
    }

    [Test]
    public async Task AttachmentRetry_AlreadyAttached_ReturnsSuccessWithoutAnotherWrite()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var member = Member(Guid.NewGuid(), Guid.NewGuid(), actor, tenant);
        var message = Message(Guid.NewGuid(), member.MessageThreadId, member.Id, tenant, "photo");
        var file = new MessageFile { Id = Guid.NewGuid(), TenantId = tenant, MessageId = message.Id, StorageId = Guid.NewGuid(), IsEnabled = true };
        var context = new InMemoryDataContext(); context.Seed(Thread(member.MessageThreadId, tenant), member, message, file);
        var result = await CreateService(context, storage: new TestStorageServiceWrapper()).CreateMessageFileAsync(new() { ThreadId = member.MessageThreadId, MessageId = message.Id, StorageFileId = file.StorageId, Metadata = Metadata(actor, tenant) });
        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(context.Set<MessageFile>(), Has.Count.EqualTo(1));
        Assert.That(context.Set<MessageOutboxEvent>(), Is.Empty);
    }
}
