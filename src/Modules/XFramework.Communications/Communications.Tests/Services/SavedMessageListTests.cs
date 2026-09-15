using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task GetSavedMessagesAsync_ReturnsOnlyTheCallersOwnSaves()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var caller = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var other = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var mine = Message(Guid.NewGuid(), thread.Id, caller.Id, tenant, "keep this");
        var theirs = Message(Guid.NewGuid(), thread.Id, other.Id, tenant, "their bookmark");
        var context = new InMemoryDataContext();
        context.Seed(thread, caller, other, mine, theirs,
            Saved(mine.Id, caller.Id, tenant), Saved(theirs.Id, other.Id, tenant));

        var result = await CreateService(context).GetSavedMessagesAsync(new GetSavedMessagesRequest
        { Metadata = Metadata(caller.CredentialId, tenant) });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TotalCount, Is.EqualTo(1));
        Assert.That(result.Data.Items.Single().MessageId, Is.EqualTo(mine.Id));
        Assert.That(result.Data.Items.Single().ThreadName, Is.EqualTo(thread.Name));
    }

    [Test]
    public async Task GetSavedMessagesAsync_SpansThreadsAndKeepsThreadRepliesWithTheirParent()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var first = Thread(Guid.NewGuid(), tenant); var second = Thread(Guid.NewGuid(), tenant);
        var here = Member(Guid.NewGuid(), first.Id, actor, tenant);
        var there = Member(Guid.NewGuid(), second.Id, actor, tenant);
        var timeline = Message(Guid.NewGuid(), first.Id, here.Id, tenant, "timeline note");
        var root = Message(Guid.NewGuid(), second.Id, there.Id, tenant, "root");
        var reply = Message(Guid.NewGuid(), second.Id, there.Id, tenant, "thread reply worth keeping");
        reply.ParentMessageId = root.Id; reply.IsThreadReply = true;
        var context = new InMemoryDataContext();
        context.Seed(first, second, here, there, timeline, root, reply,
            Saved(timeline.Id, here.Id, tenant), Saved(reply.Id, there.Id, tenant));

        var result = await CreateService(context).GetSavedMessagesAsync(new GetSavedMessagesRequest
        { Metadata = Metadata(actor, tenant) });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.Items.Select(x => x.MessageId), Is.EquivalentTo(new[] { timeline.Id, reply.Id }));
        var saved = result.Data.Items.Single(x => x.MessageId == reply.Id);
        Assert.Multiple(() =>
        {
            Assert.That(saved.IsThreadReply, Is.True);
            Assert.That(saved.ParentMessageId, Is.EqualTo(root.Id));
            Assert.That(saved.ThreadId, Is.EqualTo(second.Id));
        });
    }

    [Test]
    public async Task GetSavedMessagesAsync_LostAccessToTheConversation_DropsTheSave()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "no longer mine to read");
        var context = new InMemoryDataContext();
        context.Seed(thread, member, message, Saved(message.Id, member.Id, tenant));
        var service = CreateService(context);

        Assert.That((await service.GetSavedMessagesAsync(new GetSavedMessagesRequest
        { Metadata = Metadata(member.CredentialId, tenant) })).Data!.TotalCount, Is.EqualTo(1));

        member.IsEnabled = false; // Left the conversation, or was removed from it.
        var afterLeaving = await service.GetSavedMessagesAsync(new GetSavedMessagesRequest
        { Metadata = Metadata(member.CredentialId, tenant) });

        Assert.That(afterLeaving.IsSuccess, Is.True, afterLeaving.Message);
        Assert.That(afterLeaving.Data!.Items, Is.Empty);
        Assert.That(afterLeaving.Data.TotalCount, Is.Zero);
    }

    [TestCase("deleted")]
    [TestCase("hidden")]
    [TestCase("blocked")]
    [TestCase("unsaved")]
    public async Task GetSavedMessagesAsync_UnreadableMessage_IsExcluded(string scenario)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, actor, tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "was readable once");
        var save = Saved(message.Id, member.Id, tenant);
        var context = new InMemoryDataContext();
        context.Seed(thread, member, sender, message, save);
        switch (scenario)
        {
            case "deleted": message.IsDeleted = true; break;
            case "hidden": context.Seed(new MessageHidden { Id = Guid.NewGuid(), TenantId = tenant, MessageId = message.Id,
                MessageThreadMemberId = member.Id, IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid() }); break;
            case "blocked": context.Seed(Block(Guid.NewGuid(), actor, sender.CredentialId, tenant)); break;
            case "unsaved": save.IsDeleted = true; save.IsEnabled = false; break;
        }

        var result = await CreateService(context).GetSavedMessagesAsync(new GetSavedMessagesRequest
        { Metadata = Metadata(actor, tenant) });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.Items, Is.Empty);
        Assert.That(result.Data.TotalCount, Is.Zero);
    }

    [Test]
    public async Task GetSavedMessagesAsync_PagesNewestSaveFirst_AfterVisibilityFiltering()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, actor, tenant);
        var context = new InMemoryDataContext();
        context.Seed(thread, member);
        var ordered = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            var message = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, $"note {index}");
            var save = Saved(message.Id, member.Id, tenant);
            save.CreatedAt = DateTime.UtcNow.AddMinutes(-index);
            context.Seed(message, save);
            ordered.Add(message.Id);
        }
        // An invisible row must not consume a slot in the page or the total.
        var hidden = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "hidden");
        hidden.IsEnabled = false;
        context.Seed(hidden, Saved(hidden.Id, member.Id, tenant));
        var service = CreateService(context);

        var first = await service.GetSavedMessagesAsync(new GetSavedMessagesRequest
        { PageIndex = 0, PageSize = 2, Metadata = Metadata(actor, tenant) });
        var second = await service.GetSavedMessagesAsync(new GetSavedMessagesRequest
        { PageIndex = 1, PageSize = 2, Metadata = Metadata(actor, tenant) });

        Assert.Multiple(() =>
        {
            Assert.That(first.Data!.TotalCount, Is.EqualTo(5));
            Assert.That(first.Data.Items.Select(x => x.MessageId), Is.EqualTo(ordered.Take(2)));
            Assert.That(second.Data!.Items.Select(x => x.MessageId), Is.EqualTo(ordered.Skip(2).Take(2)));
        });
    }

    private static MessageSaved Saved(Guid messageId, Guid memberId, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        MessageId = messageId,
        MessageThreadMemberId = memberId,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };
}
