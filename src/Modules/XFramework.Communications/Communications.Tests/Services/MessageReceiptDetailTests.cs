using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Receipts;
using Communications.Tests.Infrastructure;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    /// <summary>The counts on the message projection already exist; what did not was *when*. A read row
    /// is the delivered row promoted in place, so its CreatedAt is still the delivery and ModifiedAt
    /// is the read - getting that backwards would report every read at the moment of delivery.</summary>
    [Test]
    public async Task GetMessageReceiptsAsync_ReportsDeliveryAndReadTimesPerMember()
    {
        var tenant = Guid.NewGuid();
        var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var reader = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var recipient = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "hello");
        var read = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true };
        var delivered = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Delivered, IsEnabled = true };
        var at = DateTime.UtcNow.AddMinutes(-10);
        var promoted = Delivery(Guid.NewGuid(), reader.Id, message.Id, tenant, read.Id);
        promoted.CreatedAt = at; promoted.ModifiedAt = at.AddMinutes(4);
        var pending = Delivery(Guid.NewGuid(), recipient.Id, message.Id, tenant, delivered.Id);
        pending.CreatedAt = at.AddMinutes(1); pending.ModifiedAt = at.AddMinutes(1);
        var ownFetch = Delivery(Guid.NewGuid(), sender.Id, message.Id, tenant, read.Id);
        var context = new InMemoryDataContext();
        context.Seed(thread, sender, reader, recipient, message, read, delivered, promoted, pending, ownFetch);

        var result = await CreateService(context).GetMessageReceiptsAsync(new GetMessageReceiptsRequest
        { ThreadId = thread.Id, MessageId = message.Id, Metadata = Metadata(sender.CredentialId, tenant) });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.ReadReceiptsEnabled, Is.True);
        Assert.That(result.Data.TotalCount, Is.EqualTo(2), "The sender's own fetch is never a receipt");
        var byCredential = result.Data.Items.ToDictionary(x => x.CredentialId);
        Assert.Multiple(() =>
        {
            Assert.That(byCredential[reader.CredentialId].DeliveredAt, Is.EqualTo(at));
            Assert.That(byCredential[reader.CredentialId].ReadAt, Is.EqualTo(at.AddMinutes(4)));
            Assert.That(byCredential[recipient.CredentialId].DeliveredAt, Is.EqualTo(at.AddMinutes(1)));
            Assert.That(byCredential[recipient.CredentialId].ReadAt, Is.Null);
        });
    }

    /// <summary>The rule the message projection already enforces, now for the detail read: a receipt
    /// belongs to the sender. A member who can see the message still may not see who read it.</summary>
    [TestCase(false, 403)]
    [TestCase(true, 403)]
    public async Task GetMessageReceiptsAsync_RefusesAMessageTheCallerDidNotSend(bool outsider, int status)
    {
        var tenant = Guid.NewGuid();
        var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var other = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "hello");
        var read = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true };
        var context = new InMemoryDataContext();
        context.Seed(thread, sender, other, message, read, Delivery(Guid.NewGuid(), other.Id, message.Id, tenant, read.Id));

        var result = await CreateService(context).GetMessageReceiptsAsync(new GetMessageReceiptsRequest
        {
            ThreadId = thread.Id, MessageId = message.Id,
            Metadata = Metadata(outsider ? Guid.NewGuid() : other.CredentialId, tenant)
        });

        Assert.That(result.StatusCode, Is.EqualTo(status));
        Assert.That(result.Data, Is.Null);
    }

    /// <summary>Read receipts are switchable per conversation and per tenant. Off means no read times
    /// at all - the caller renders no read section rather than one that reads "nobody has read this".</summary>
    [Test]
    public async Task GetMessageReceiptsAsync_WithReadReceiptsOff_KeepsDeliveryButDropsEveryReadTime()
    {
        var tenant = Guid.NewGuid();
        var thread = Thread(Guid.NewGuid(), tenant);
        thread.Features &= ~ConversationFeatures.ReadReceipts;
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var reader = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "hello");
        var read = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true };
        var row = Delivery(Guid.NewGuid(), reader.Id, message.Id, tenant, read.Id);
        row.ModifiedAt = row.CreatedAt.AddMinutes(2);
        var context = new InMemoryDataContext();
        context.Seed(thread, sender, reader, message, read, row);

        var result = await CreateService(context).GetMessageReceiptsAsync(new GetMessageReceiptsRequest
        { ThreadId = thread.Id, MessageId = message.Id, Metadata = Metadata(sender.CredentialId, tenant) });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.ReadReceiptsEnabled, Is.False);
        Assert.That(result.Data.Items.Single().DeliveredAt, Is.EqualTo(row.CreatedAt));
        Assert.That(result.Data.Items.Single().ReadAt, Is.Null);
    }
}
