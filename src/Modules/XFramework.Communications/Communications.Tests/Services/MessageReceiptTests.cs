using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task ReceiptPositions_UseWholeHistory_AndReadersAdvanceIndependently()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var a = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var b = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var messages = Enumerable.Range(0, 3).Select(i => {
            var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, $"Message {i}");
            message.CreatedAt = DateTime.UtcNow.Date.AddMinutes(i); return message;
        }).ToArray();
        var read = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true };
        var delivered = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Delivered, IsEnabled = true };
        var context = new InMemoryDataContext(); context.Seed(thread, sender, a, b, read, delivered); context.Seed(messages);
        context.Seed(Delivery(Guid.NewGuid(), a.Id, messages[0].Id, tenant, read.Id),
            Delivery(Guid.NewGuid(), a.Id, messages[2].Id, tenant, read.Id),
            Delivery(Guid.NewGuid(), b.Id, messages[0].Id, tenant, read.Id),
            Delivery(Guid.NewGuid(), b.Id, messages[1].Id, tenant, read.Id));
        var service = CreateService(context);
        for (var page = 0; page < 3; page++)
        {
            var response = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, PageIndex = page, PageSize = 1, Metadata = Metadata(sender.CredentialId, tenant) });
            Assert.That(response.IsSuccess, Is.True, response.Message);
            var message = response.Data!.Items.Single();
            Assert.That(message.IsLatestOwnMessage, Is.EqualTo(page == 0));
            Assert.That(message.LatestReadCredentialIds, Is.EqualTo(page == 0 ? new[] { a.CredentialId } : page == 1 ? new[] { b.CredentialId } : Array.Empty<Guid>()));
        }
    }

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
        Assert.That(result.Data.Items.Single().ReadCredentialIds, Is.EqualTo(readEnabled ? new[] { recipient.CredentialId } : Array.Empty<Guid>()));
        Assert.That(result.Data.Items.Single().LatestReadCredentialIds, Is.EqualTo(readEnabled ? new[] { recipient.CredentialId } : Array.Empty<Guid>()));
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
            Assert.That(received.Data!.Items.Single().ReadCredentialIds, Is.Empty);
            Assert.That(received.Data!.Items.Single().DeliveredCount, Is.Zero, "Recipients must not receive other members' receipts");
        }
        Assert.That(context.Set<MessageOutboxEvent>().Single().EventType, Is.EqualTo(MessageRealtimeEvents.MessagesDelivered));
        var after = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, Metadata = Metadata(sender.CredentialId, tenant) });
        Assert.That(after.Data!.Items.Single().DeliveredCount, Is.EqualTo(1));
        Assert.That(after.Data.Items.Single().ReadCount, Is.Zero);
    }
}
