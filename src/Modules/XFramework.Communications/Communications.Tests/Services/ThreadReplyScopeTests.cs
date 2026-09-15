using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task GetThreadMessagesAsync_Timeline_KeepsInlineRepliesAndHidesThreadReplies()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var root = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "root");
        var inline = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "inline reply");
        inline.ParentMessageId = root.Id;
        var threadReply = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "thread reply");
        threadReply.ParentMessageId = root.Id; threadReply.IsThreadReply = true;
        var context = new InMemoryDataContext();
        context.Seed(thread, member, root, inline, threadReply, DeliveredType(tenant));
        var result = await CreateService(context).GetThreadMessagesAsync(new GetThreadMessagesRequest
        { ThreadId = thread.Id, Metadata = Metadata(member.CredentialId, tenant) });
        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TotalCount, Is.EqualTo(2));
        Assert.That(result.Data.Items.Select(x => x.Id), Is.EquivalentTo(new[] { root.Id, inline.Id }));
    }

    [Test]
    public async Task GetThreadMessagesAsync_ParentFilter_ReturnsThreadRepliesWithoutInlineReplies()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var root = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "root");
        var inline = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "inline reply");
        inline.ParentMessageId = root.Id;
        var threadReply = Message(Guid.NewGuid(), thread.Id, member.Id, tenant, "thread reply");
        threadReply.ParentMessageId = root.Id; threadReply.IsThreadReply = true;
        var context = new InMemoryDataContext();
        context.Seed(thread, member, root, inline, threadReply, DeliveredType(tenant));
        var result = await CreateService(context).GetThreadMessagesAsync(new GetThreadMessagesRequest
        { ThreadId = thread.Id, ParentMessageId = root.Id, Metadata = Metadata(member.CredentialId, tenant) });
        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TotalCount, Is.EqualTo(1));
        Assert.That(result.Data.Items.Single().Id, Is.EqualTo(threadReply.Id));
    }

    [Test]
    public async Task GetThreadListAsync_PreviewAndUnreadCount_IgnoreThreadReplies()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var member = Member(Guid.NewGuid(), thread.Id, actor, tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var timeline = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "timeline");
        var threadReply = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "thread reply");
        threadReply.CreatedAt = timeline.CreatedAt.AddMinutes(1);
        threadReply.ParentMessageId = timeline.Id; threadReply.IsThreadReply = true;
        var context = new InMemoryDataContext();
        context.Seed(thread, member, sender, timeline, threadReply);
        var result = await CreateService(context).GetThreadListAsync(new GetThreadListRequest { Metadata = Metadata(actor, tenant) });
        Assert.That(result.IsSuccess, Is.True, result.Message);
        var item = result.Data!.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.LastMessagePreview, Is.EqualTo("timeline"));
            Assert.That(item.LastMessageAt, Is.EqualTo(timeline.CreatedAt));
            Assert.That(item.UnreadCount, Is.EqualTo(1));
        });
    }

    private static MessageDeliveryType DeliveredType(Guid tenant) => new()
    { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Delivered, Name = "Delivered", IsEnabled = true };
}
