using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Requests.Edit;
using IdentityServer.Domain.Shared.Contracts;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test]
    public async Task DeferredEncryption_DeliversToReadyMembers_ThenCatchesUpOriginalAudienceOnly()
    {
        var (context, send, sender, ready) = EncryptionScenario();
        var late = Member(Guid.NewGuid(), send.ThreadId, Guid.NewGuid(), sender.TenantId);
        context.Seed(late);
        var service = CreateService(context);
        send.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        Assert.That((await service.CreateThreadMessageAsync(send)).IsSuccess, Is.True);
        var stored = context.Set<Message>().Single();
        Assert.That(stored.PendingEncryptionCount, Is.EqualTo(1));
        Assert.That(JsonSerializer.Deserialize<List<Guid>>(stored.PendingEncryptionMembersJson), Is.EqualTo(new[] { late.Id }));
        var senderPage = await service.GetDeferredEncryptionAsync(new() { Metadata = send.Metadata });
        Assert.That(senderPage.Data!.Items, Has.Count.EqualTo(1));
        Assert.That(senderPage.Data.Items[0].PendingCredentialIds, Is.EqualTo(new[] { late.CredentialId }));
        Assert.That((await service.GetDeferredEncryptionAsync(new() { Metadata = Metadata(ready.CredentialId, ready.TenantId) })).Data!.Items, Is.Empty);
        context.Seed(new EncryptionAccount { TenantId = late.TenantId, CredentialId = late.CredentialId, DirectoryRevision = 1, DevicesJson = Devices(Guid.NewGuid()) });
        var newcomer = Member(Guid.NewGuid(), send.ThreadId, Guid.NewGuid(), sender.TenantId);
        context.Seed(newcomer, new EncryptionAccount { TenantId = newcomer.TenantId, CredentialId = newcomer.CredentialId, DirectoryRevision = 1, DevicesJson = Devices(Guid.NewGuid()) });
        var complete = new CompleteDeferredEncryptionRequest
        {
            Metadata = Metadata(sender.CredentialId, sender.TenantId), ThreadId = send.ThreadId, MessageId = stored.Id,
            ExpectedEnvelopeHash = senderPage.Data.Items[0].EnvelopeHash, EncryptedEnvelope = Envelope('c'),
            EncryptionSenderDeviceId = send.EncryptionSenderDeviceId!.Value, SenderDirectoryRevision = 1,
            RecipientDirectoryRevisions = new(send.RecipientDirectoryRevisions) { [late.CredentialId] = 1 }
        };
        complete.RecipientDirectoryRevisions[newcomer.CredentialId] = 1;
        Assert.That((await service.CompleteDeferredEncryptionAsync(complete)).StatusCode, Is.EqualTo(412), "New members must not gain historical access.");
        complete.RecipientDirectoryRevisions.Remove(newcomer.CredentialId);
        complete.Metadata = Metadata(ready.CredentialId, ready.TenantId);
        Assert.That((await service.CompleteDeferredEncryptionAsync(complete)).StatusCode, Is.EqualTo(403));
        complete.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        stored.CreatedAt = DateTime.UtcNow.AddDays(-30); // Delivery is not constrained by the editing window.
        Assert.That((await service.CompleteDeferredEncryptionAsync(complete)).IsSuccess, Is.True);
        Assert.That(stored.PendingEncryptionCount, Is.Zero);
        Assert.That(stored.EncryptedEnvelope, Is.EqualTo(complete.EncryptedEnvelope));
        Assert.That((await service.CompleteDeferredEncryptionAsync(complete)).IsSuccess, Is.True, "Lost response retry is idempotent.");
        Assert.That((await service.CreateThreadMessageAsync(send)).IsSuccess, Is.True, "The original create retry still acknowledges its accepted ciphertext.");
        Assert.That((await service.GetDeferredEncryptionAsync(new() { Metadata = send.Metadata })).Data!.Items, Is.Empty);
    }

    [Test]
    public async Task DeferredEncryption_RejectsOmittedReadyRecipient_AndStaleCatchupAfterEdit()
    {
        var (context, send, sender, ready) = EncryptionScenario();
        var late = Member(Guid.NewGuid(), send.ThreadId, Guid.NewGuid(), sender.TenantId);
        context.Seed(late);
        var service = CreateService(context);
        send.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        send.RecipientCredentialIds.Remove(ready.CredentialId); send.RecipientDirectoryRevisions.Remove(ready.CredentialId);
        Assert.That((await service.CreateThreadMessageAsync(send)).StatusCode, Is.EqualTo(412));
        send.RecipientCredentialIds.Add(ready.CredentialId); send.RecipientDirectoryRevisions[ready.CredentialId] = 1;
        Assert.That((await service.CreateThreadMessageAsync(send)).IsSuccess, Is.True);
        var pending = (await service.GetDeferredEncryptionAsync(new() { Metadata = send.Metadata })).Data!.Items.Single();
        var edit = new EditThreadMessageRequest { Metadata = send.Metadata, ThreadId = send.ThreadId, MessageId = pending.Message.Id,
            Text = EncryptedMessages.Preview, EncryptedEnvelope = Envelope('b'), EncryptionSenderDeviceId = send.EncryptionSenderDeviceId!.Value,
            SenderDirectoryRevision = 1, RecipientDirectoryRevisions = send.RecipientDirectoryRevisions };
        Assert.That((await service.EditThreadMessageAsync(edit)).IsSuccess, Is.True);
        Assert.That((await service.CompleteDeferredEncryptionAsync(new() { Metadata = send.Metadata, ThreadId = send.ThreadId,
            MessageId = pending.Message.Id, ExpectedEnvelopeHash = pending.EnvelopeHash, EncryptedEnvelope = Envelope('c'),
            EncryptionSenderDeviceId = send.EncryptionSenderDeviceId!.Value, SenderDirectoryRevision = 1,
            RecipientDirectoryRevisions = send.RecipientDirectoryRevisions })).StatusCode, Is.EqualTo(409));
        Assert.That(context.Set<Message>().Single().EncryptedEnvelope, Is.EqualTo(edit.EncryptedEnvelope));
    }

    [Test]
    public async Task DeferredEncryption_PendingMemberCannotReportDeliveredOrRead()
    {
        var (context, send, sender, _) = EncryptionScenario();
        var late = Member(Guid.NewGuid(), send.ThreadId, Guid.NewGuid(), sender.TenantId);
        context.Seed(late);
        var service = CreateService(context);
        send.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        Assert.That((await service.CreateThreadMessageAsync(send)).IsSuccess, Is.True);
        var page = await service.GetThreadMessagesAsync(new() { ThreadId = send.ThreadId, Metadata = Metadata(late.CredentialId, late.TenantId) });
        Assert.That(page.IsSuccess, Is.True, page.Message);
        Assert.That(page.Data!.Items.Single().EncryptionPending, Is.True);
        Assert.That(context.Set<MessageDelivery>().Any(x => x.MessageThreadMemberId == late.Id), Is.False);
        context.Seed(new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = late.TenantId,
            SystemReferenceId = MessageDeliveryTypes.Read, Name = "Read", IsEnabled = true });
        var read = await service.MarkMessagesReadAsync(new() { ThreadId = send.ThreadId, MessageIds = [send.ClientMessageId!.Value], Metadata = Metadata(late.CredentialId, late.TenantId) });
        Assert.That(read.IsSuccess, Is.True, read.Message);
        Assert.That(context.Set<MessageDelivery>().Any(x => x.MessageThreadMemberId == late.Id), Is.False);
    }
    [Test]
    public async Task DeferredEncryption_ReaddedMembershipDoesNotReceiveHistoricalKeysOrReceipts()
    {
        var (context, send, sender, _) = EncryptionScenario();
        var late = Member(Guid.NewGuid(), send.ThreadId, Guid.NewGuid(), sender.TenantId);
        context.Seed(late);
        var service = CreateService(context); send.Metadata = Metadata(sender.CredentialId, sender.TenantId);
        Assert.That((await service.CreateThreadMessageAsync(send)).IsSuccess, Is.True);
        late.IsDeleted = true;
        var rejoined = Member(Guid.NewGuid(), send.ThreadId, late.CredentialId, late.TenantId);
        context.Seed(rejoined, new EncryptionAccount { TenantId = late.TenantId, CredentialId = late.CredentialId, DirectoryRevision = 1, DevicesJson = Devices(Guid.NewGuid()) });
        var pending = (await service.GetDeferredEncryptionAsync(new() { Metadata = send.Metadata })).Data!.Items.Single();
        Assert.That(pending.PendingCredentialIds, Is.Empty);
        Assert.That(pending.Message.EncryptionAudienceCredentialIds, Does.Not.Contain(late.CredentialId));
        Assert.That((await service.CompleteDeferredEncryptionAsync(new() { Metadata = send.Metadata, ThreadId = send.ThreadId,
            MessageId = pending.Message.Id, ExpectedEnvelopeHash = pending.EnvelopeHash, EncryptedEnvelope = Envelope('d'),
            EncryptionSenderDeviceId = send.EncryptionSenderDeviceId!.Value, SenderDirectoryRevision = 1,
            RecipientDirectoryRevisions = send.RecipientDirectoryRevisions })).IsSuccess, Is.True);
        Assert.That(context.Set<Message>().Single().PendingEncryptionCount, Is.Zero);
        Assert.That((await service.GetThreadMessagesAsync(new() { ThreadId = send.ThreadId, Metadata = Metadata(late.CredentialId, late.TenantId) })).IsSuccess, Is.True);
        Assert.That(context.Set<MessageDelivery>().Any(d => d.MessageThreadMemberId == rejoined.Id), Is.False);
    }

    [Test, Category("Kind:Integration")]
    public async Task DeferredEncryption_ConcurrentCatchupAcrossConnections_OnlyOneEnvelopeWins()
    {
        await using var postgres = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DeferredDb>().UseNpgsql(postgres.GetConnectionString())
            .UseQueryTrackingBehavior(Microsoft.EntityFrameworkCore.QueryTrackingBehavior.NoTracking).Options;
        var (memory, send, sender, _) = EncryptionScenario();
        var members = memory.Set<MessageThreadMember>();
        var late = Member(Guid.NewGuid(), send.ThreadId, Guid.NewGuid(), sender.TenantId); members.Add(late);
        var message = Message(send.ClientMessageId!.Value, send.ThreadId, members[0].Id, sender.TenantId, EncryptedMessages.Preview);
        message.EncryptedEnvelope = send.EncryptedEnvelope; message.EncryptionSenderDeviceId = send.EncryptionSenderDeviceId;
        message.AcceptedSenderDirectoryRevision = 1; message.PendingEncryptionCount = 1;
        message.EncryptionAudienceJson = JsonSerializer.Serialize(members.Select(m => m.Id));
        message.PendingEncryptionMembersJson = JsonSerializer.Serialize(new[] { late.Id });
        await using (var seed = new DeferredDb(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.AddRange(members); seed.Add(memory.Set<MessageThread>().Single()); seed.Add(message);
            seed.AddRange(memory.Set<EncryptionAccount>());
            seed.Add(new EncryptionAccount { TenantId = sender.TenantId, CredentialId = late.CredentialId, DirectoryRevision = 1, DevicesJson = Devices(Guid.NewGuid()) });
            await seed.SaveChangesAsync();
        }
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(message.EncryptedEnvelope!)));
        var results = await Task.WhenAll(new[] { 'b', 'c' }.Select(async value =>
        {
            await using var db = new DeferredDb(options);
            var service = CreateService(new XFramework.Core.DataContext.ServerDataContext<DeferredDb>(db), database: db);
            return await service.CompleteDeferredEncryptionAsync(new()
            {
                Metadata = Metadata(sender.CredentialId, sender.TenantId), ThreadId = send.ThreadId, MessageId = message.Id,
                ExpectedEnvelopeHash = hash, EncryptedEnvelope = Envelope(value), EncryptionSenderDeviceId = send.EncryptionSenderDeviceId!.Value,
                SenderDirectoryRevision = 1, RecipientDirectoryRevisions = new(send.RecipientDirectoryRevisions) { [late.CredentialId] = 1 }
            });
        }));
        Assert.That(results.Count(r => r.IsSuccess), Is.EqualTo(1), string.Join("; ", results.Select(r => r.Message)));
        Assert.That(results.Count(r => r.StatusCode == 409), Is.EqualTo(1));
        await using var check = new DeferredDb(options);
        Assert.That((await check.Set<Message>().SingleAsync()).PendingEncryptionCount, Is.Zero);
        Assert.That(await check.Set<MessageOutboxEvent>().CountAsync(), Is.EqualTo(1));
    }

    private sealed class DeferredDb(Microsoft.EntityFrameworkCore.DbContextOptions<DeferredDb> options) : Microsoft.EntityFrameworkCore.DbContext(options)
    {
        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder model)
        {
            model.Entity<Message>().Ignore(x => x.MessageDeliveries).Ignore(x => x.MessageFiles).Ignore(x => x.MessageReactions)
                .Ignore(x => x.MessageThread).Ignore(x => x.MessageThreadMember).Ignore(x => x.ParentMessage).Ignore(x => x.Replies);
            model.Entity<MessageThreadMember>().Ignore(x => x.Group).Ignore(x => x.Credential).Ignore(x => x.MessageDeliveries)
                .Ignore(x => x.MessageThread).Ignore(x => x.MessageThreadMemberRoles).Ignore(x => x.Messages);
            model.Entity<MessageThread>().Ignore(x => x.Type).Ignore(x => x.MessageThreadMemberGroups).Ignore(x => x.MessageThreadMembers).Ignore(x => x.Messages);
            model.Entity<MessageOutboxEvent>();
            model.Entity<EncryptionAccount>().HasKey(x => new { x.TenantId, x.CredentialId });
        }
    }

}
