using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task MessageProjection_DoesNotAcknowledge_DeliveryIsIdempotentAndNeverDowngradesRead()
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var recipient = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "ciphertext preview");
        message.EncryptedEnvelope = "opaque encrypted envelope";
        var delivered = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Delivered, IsEnabled = true };
        var read = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true };
        var context = new InMemoryDataContext(); context.Seed(thread, sender, recipient, message, delivered, read);
        var service = CreateService(context);
        var projection = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id],
            SuppressDeliveryAcknowledgement = true, Metadata = Metadata(recipient.CredentialId, tenant) });
        Assert.That(projection.IsSuccess, Is.True, projection.Message);
        Assert.That(projection.Data!.Items.Single().EncryptedEnvelope, Is.EqualTo(message.EncryptedEnvelope));
        Assert.That(context.Set<MessageDelivery>(), Is.Empty);
        Assert.That(context.Set<MessageOutboxEvent>(), Is.Empty);
        for (var i = 0; i < 2; i++)
            Assert.That((await service.MarkMessagesDeliveredAsync(new() { ThreadId = thread.Id,
                MessageIds = [message.Id, message.Id], Metadata = Metadata(recipient.CredentialId, tenant) })).IsSuccess, Is.True);
        Assert.That(context.Set<MessageDelivery>().Single().TypeId, Is.EqualTo(delivered.Id));
        Assert.That(context.Set<MessageOutboxEvent>().Single().EventType, Is.EqualTo(MessageRealtimeEvents.MessagesDelivered));
        Assert.That((await service.MarkMessagesReadAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id],
            Metadata = Metadata(recipient.CredentialId, tenant) })).IsSuccess, Is.True);
        await service.MarkMessagesDeliveredAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id], Metadata = Metadata(recipient.CredentialId, tenant) });
        Assert.That(context.Set<MessageDelivery>().Single().TypeId, Is.EqualTo(read.Id));
        Assert.That(context.Set<MessageOutboxEvent>(), Has.Count.EqualTo(2));
        thread.Features &= ~ConversationFeatures.ReadReceipts;
        var senderView = await service.GetThreadMessagesAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id],
            SuppressDeliveryAcknowledgement = true, Metadata = Metadata(sender.CredentialId, tenant) });
        Assert.That(senderView.Data!.Items.Single().DeliveredCount, Is.EqualTo(1));
        Assert.That(senderView.Data.Items.Single().ReadCount, Is.Zero);
        Assert.That(senderView.Data.Items.Single().ReadCredentialIds, Is.Empty);
    }

    [TestCase("nonmember", 403)]
    [TestCase("wrongtenant", 403)]
    [TestCase("disabledmember", 403)]
    [TestCase("foreignmessage", 404)]
    [TestCase("otherthread", 404)]
    [TestCase("deleted", 404)]
    [TestCase("hidden", 404)]
    [TestCase("blocked", 404)]
    [TestCase("pending", 200)]
    [TestCase("outsideaudience", 200)]
    [TestCase("own", 200)]
    public async Task ExplicitDelivery_DoesNotAcknowledgeInaccessibleOrUnreceivedMessages(string scenario, int status)
    {
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var recipient = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "hello");
        var context = new InMemoryDataContext(); context.Seed(thread, sender, message);
        if (scenario != "nonmember") context.Seed(recipient);
        if (scenario == "disabledmember") recipient.IsEnabled = false;
        if (scenario == "foreignmessage") message.TenantId = Guid.NewGuid();
        if (scenario == "otherthread") message.MessageThreadId = Guid.NewGuid();
        if (scenario == "deleted") message.IsDeleted = true;
        if (scenario == "hidden") context.Seed(new MessageHidden { Id = Guid.NewGuid(), TenantId = tenant,
            MessageId = message.Id, MessageThreadMemberId = recipient.Id, IsEnabled = true });
        if (scenario == "blocked") context.Seed(Block(Guid.NewGuid(), sender.CredentialId, recipient.CredentialId, tenant));
        if (scenario == "pending") message.PendingEncryptionMembersJson = $"[\"{recipient.Id}\"]";
        if (scenario == "outsideaudience") message.EncryptionAudienceJson = $"[\"{sender.Id}\"]";
        if (scenario == "own") message.MessageThreadMemberId = recipient.Id;
        var result = await CreateService(context).MarkMessagesDeliveredAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id],
            RequesterCredentialId = sender.CredentialId, Metadata = Metadata(recipient.CredentialId, scenario == "wrongtenant" ? Guid.NewGuid() : tenant) });
        Assert.That(result.StatusCode, Is.EqualTo(status), result.Message);
        Assert.That(context.Set<MessageDelivery>(), Is.Empty);
        Assert.That(context.Set<MessageOutboxEvent>(), Is.Empty);
        if (scenario is "hidden" or "blocked")
        {
            var projected = await CreateService(context).GetThreadMessagesAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id],
                SuppressDeliveryAcknowledgement = true, Metadata = Metadata(recipient.CredentialId, tenant) });
            Assert.That(projected.IsSuccess, Is.True, projected.Message);
            Assert.That(projected.Data!.Items, Is.Empty);
        }
    }

    [TestCase(0)]
    [TestCase(51)]
    public async Task ExplicitDelivery_RejectsUnboundedRequests(int count)
    {
        var result = await CreateService(new InMemoryDataContext()).MarkMessagesDeliveredAsync(new()
        { ThreadId = Guid.NewGuid(), MessageIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList() });
        Assert.That(result.StatusCode, Is.EqualTo(400));
    }
}
