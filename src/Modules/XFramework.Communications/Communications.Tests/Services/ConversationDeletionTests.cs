using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task DeleteConversation_RequiresSameTenantAdmin_RevokesMembers_AndReturnsPrivateTombstones()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var admin = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant); admin.Role = MessageThreadMemberRoles.Admin;
        var member = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, admin.Id, tenant, "private history");
        var context = new InMemoryDataContext(); context.Seed(thread, admin, member, message);
        var service = CreateService(context);
        Assert.That((await service.DeleteThreadAsync(new() { ThreadId = thread.Id, Metadata = Metadata(member.CredentialId, tenant) })).StatusCode, Is.EqualTo(403));
        Assert.That((await service.DeleteThreadAsync(new() { ThreadId = thread.Id, Metadata = Metadata(admin.CredentialId, Guid.NewGuid()) })).StatusCode, Is.EqualTo(404));
        Assert.That(thread.IsDeleted, Is.False);
        Assert.That(context.Set<MessageOutboxEvent>(), Is.Empty);
        var result = await service.DeleteThreadAsync(new() { ThreadId = thread.Id, Metadata = Metadata(admin.CredentialId, tenant) });
        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(thread.IsDeleted, Is.True);
        Assert.That(context.Set<MessageThreadMember>().All(m => m.IsDeleted && !m.IsEnabled), Is.True);
        Assert.That(context.Set<MessageOutboxEvent>().Single().EventType, Is.EqualTo(MessageRealtimeEvents.ThreadDeleted));
        Assert.That((await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, Metadata = Metadata(member.CredentialId, tenant) })).StatusCode, Is.EqualTo(403));
        var tombstones = await service.GetDeletedThreadsAsync(new() { Metadata = Metadata(member.CredentialId, tenant) });
        Assert.That(tombstones.Data!.Items, Is.EqualTo(new[] { thread.Id }));
        Assert.That((await service.GetDeletedThreadsAsync(new() { Metadata = Metadata(Guid.NewGuid(), tenant) })).Data!.Items, Is.Empty);
        Assert.That((await service.GetDeletedThreadsAsync(new() { Metadata = Metadata(member.CredentialId, Guid.NewGuid()) })).Data!.Items, Is.Empty);
        Assert.That((await service.GetThreadListAsync(new() { Metadata = Metadata(member.CredentialId, tenant) })).Data!.Items, Is.Empty);
        Assert.That((await service.SearchMessagesAsync(new() { Query = "private", Metadata = Metadata(member.CredentialId, tenant) })).Data!.Items, Is.Empty);
    }

    [Test]
    public async Task DeleteDirectConversation_DeletesPairIndex_AndTombstonesArePaged()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var context = new InMemoryDataContext();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var thread = Thread(Guid.NewGuid(), tenant); ids.Add(thread.Id);
            var member = Member(Guid.NewGuid(), thread.Id, actor, tenant); member.Role = MessageThreadMemberRoles.Admin;
            context.Seed(thread, member, new MessageDirectThread { Id = Guid.NewGuid(), TenantId = tenant, MessageThreadId = thread.Id, IsEnabled = true });
            Assert.That((await CreateService(context).DeleteThreadAsync(new() { ThreadId = thread.Id, Metadata = Metadata(actor, tenant) })).IsSuccess, Is.True);
        }
        Assert.That(context.Set<MessageDirectThread>().All(d => d.IsDeleted && !d.IsEnabled), Is.True);
        var service = CreateService(context);
        var first = await service.GetDeletedThreadsAsync(new() { PageSize = 2, Metadata = Metadata(actor, tenant) });
        var second = await service.GetDeletedThreadsAsync(new() { PageSize = 2, PageIndex = 1, Metadata = Metadata(actor, tenant) });
        Assert.That(first.Data!.TotalCount, Is.EqualTo(3));
        Assert.That(first.Data.Items, Has.Count.EqualTo(2));
        Assert.That(first.Data.Items.Concat(second.Data!.Items), Is.EquivalentTo(ids));
    }
}
