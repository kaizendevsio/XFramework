using Communications.Tests.Infrastructure;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task MessageUpdates_ReturnOnlyRequestedVisibleMessages_AndKeepMembershipBoundary()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var selected = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "selected");
        var unselected = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "unselected");
        var foreign = Message(Guid.NewGuid(), thread.Id, sender.Id, Guid.NewGuid(), "foreign");
        var otherThread = Message(Guid.NewGuid(), Guid.NewGuid(), sender.Id, tenant, "other thread");
        var context = new InMemoryDataContext(); context.Seed(thread, sender, selected, unselected, foreign, otherThread);
        context.Seed(new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Delivered, IsEnabled = true },
            new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true });
        var service = CreateService(context);
        var response = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id,
            MessageIds = [selected.Id, foreign.Id, otherThread.Id], Metadata = Metadata(sender.CredentialId, tenant) });
        Assert.That(response.IsSuccess, Is.True, response.Message);
        Assert.That(response.Data!.Items.Select(m => m.Id), Is.EqualTo(new[] { selected.Id }));
        selected.IsDeleted = true;
        Assert.That((await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, MessageIds = [selected.Id],
            Metadata = Metadata(sender.CredentialId, tenant) })).Data!.Items, Is.Empty);
        Assert.That((await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, MessageIds = [selected.Id],
            Metadata = Metadata(Guid.NewGuid(), tenant) })).StatusCode, Is.EqualTo(403));
        Assert.That((await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id,
            MessageIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray(), Metadata = Metadata(sender.CredentialId, tenant) })).StatusCode, Is.EqualTo(400));
    }
}
