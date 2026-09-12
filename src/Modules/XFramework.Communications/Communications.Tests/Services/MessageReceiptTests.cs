using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task MessageReceipts_CountRecipients_NotSenderOrForeignTenant_RespectReadSetting(bool readEnabled)
    {
        var tenant = Guid.NewGuid();
        var thread = Thread(Guid.NewGuid(), tenant);
        if (!readEnabled) thread.Features &= ~ConversationFeatures.ReadReceipts;
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var recipient = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var second = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "hello");
        var delivered = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Delivered, IsEnabled = true };
        var read = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true };
        var context = new InMemoryDataContext();
        context.Seed(thread, sender, recipient, second, message, delivered, read,
            Delivery(Guid.NewGuid(), sender.Id, message.Id, tenant, read.Id),
            Delivery(Guid.NewGuid(), recipient.Id, message.Id, tenant, read.Id),
            Delivery(Guid.NewGuid(), recipient.Id, message.Id, tenant, delivered.Id),
            Delivery(Guid.NewGuid(), second.Id, message.Id, tenant, delivered.Id),
            Delivery(Guid.NewGuid(), Guid.NewGuid(), message.Id, Guid.NewGuid(), read.Id));
        var result = await CreateService(context).GetThreadMessagesAsync(new()
        { ThreadId = thread.Id, Metadata = Metadata(sender.CredentialId, tenant) });
        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.Items.Single().DeliveredCount, Is.EqualTo(2));
        Assert.That(result.Data.Items.Single().ReadCount, Is.EqualTo(readEnabled ? 1 : 0));
    }

    [Test]
    public async Task RecipientFetch_PublishesDeliveryOnce_AndSenderSeesDelivered()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var recipient = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "hello");
        var delivered = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Delivered, IsEnabled = true };
        var context = new InMemoryDataContext(); context.Seed(thread, sender, recipient, message, delivered);
        var service = CreateService(context);
        var before = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, Metadata = Metadata(sender.CredentialId, tenant) });
        Assert.That(before.Data!.Items.Single().DeliveredCount, Is.Zero);
        Assert.That(context.Set<MessageOutboxEvent>(), Is.Empty);
        for (var i = 0; i < 2; i++)
        {
            var received = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, Metadata = Metadata(recipient.CredentialId, tenant) });
            Assert.That(received.IsSuccess, Is.True, received.Message);
            Assert.That(received.Data!.Items.Single().DeliveredCount, Is.Zero, "Recipients must not receive other members' receipts");
        }
        Assert.That(context.Set<MessageOutboxEvent>().Single().EventType, Is.EqualTo(MessageRealtimeEvents.MessagesDelivered));
        var after = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, Metadata = Metadata(sender.CredentialId, tenant) });
        Assert.That(after.Data!.Items.Single().DeliveredCount, Is.EqualTo(1));
        Assert.That(after.Data.Items.Single().ReadCount, Is.Zero);
    }
}
