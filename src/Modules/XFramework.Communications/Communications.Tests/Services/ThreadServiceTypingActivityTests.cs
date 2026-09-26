using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task TypingActivity_CarriesKindAndClampedCount_UnderTheTypingSetting()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var context = new InMemoryDataContext(); context.Seed(thread, Member(Guid.NewGuid(), thread.Id, actor, tenant));
        var publisher = new TestTransientRealtimePublisher();
        var service = CreateService(context, publisher: publisher);
        var metadata = Metadata(actor, tenant);

        var photos = await service.PublishTypingAsync(new() { ThreadId = thread.Id, IsTyping = true, Activity = CommunicationsTypingActivity.Photo, Count = 500, Metadata = metadata });
        var typing = await service.PublishTypingAsync(new() { ThreadId = thread.Id, IsTyping = true, Count = 4, Metadata = metadata });
        var recording = await service.PublishTypingAsync(new() { ThreadId = thread.Id, IsTyping = true, Activity = CommunicationsTypingActivity.Recording, Count = 2, Metadata = metadata });
        Assert.That(new[] { photos.StatusCode, typing.StatusCode, recording.StatusCode }, Is.All.EqualTo(202), photos.Message);
        Assert.That(publisher.Typing.Select(x => (x.Activity, x.Count, x.CredentialId)), Is.EqualTo(new[]
        {
            (CommunicationsTypingActivity.Photo, 99, actor), (CommunicationsTypingActivity.Typing, 0, actor), (CommunicationsTypingActivity.Recording, 0, actor)
        }));

        Assert.That((await service.PublishTypingAsync(new() { ThreadId = thread.Id, IsTyping = true, Activity = (CommunicationsTypingActivity)42, Metadata = metadata })).StatusCode, Is.EqualTo(400));
        thread.Features &= ~ConversationFeatures.Typing;
        Assert.That((await service.PublishTypingAsync(new() { ThreadId = thread.Id, IsTyping = true, Activity = CommunicationsTypingActivity.Video, Count = 1, Metadata = metadata })).StatusCode,
            Is.EqualTo(403), "Typing off in a conversation also withholds attachment activity.");
        Assert.That(publisher.Typing, Has.Count.EqualTo(3));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task ThreadList_SaysWhetherTheDirectPeerSharesActiveStatus(bool peerHides, bool expected)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var peer = Guid.NewGuid();
        var direct = Thread(Guid.NewGuid(), tenant); var group = Thread(Guid.NewGuid(), tenant);
        var pair = new[] { actor, peer }.Order().ToArray();
        var peerMember = Member(Guid.NewGuid(), direct.Id, peer, tenant); peerMember.HideActiveStatus = peerHides;
        var context = new InMemoryDataContext();
        context.Seed(direct, group, Member(Guid.NewGuid(), direct.Id, actor, tenant), peerMember,
            Member(Guid.NewGuid(), group.Id, actor, tenant), Member(Guid.NewGuid(), group.Id, peer, tenant),
            new MessageDirectThread { Id = Guid.NewGuid(), TenantId = tenant, MessageThreadId = direct.Id, FirstCredentialId = pair[0], SecondCredentialId = pair[1],
                IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid() });

        var list = (await CreateService(context).GetThreadListAsync(new GetThreadListRequest { Metadata = Metadata(actor, tenant) })).Data!.Items;
        Assert.That(list.Single(x => x.Id == direct.Id).OtherSharesActiveStatus, Is.EqualTo(expected));
        Assert.That(list.Single(x => x.Id == group.Id).OtherSharesActiveStatus, Is.False, "A group has no single peer whose status could be shared.");
    }
}
