using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using IdentityServer.Domain.Shared.Contracts;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test, Category("Kind:Integration")]
    public async Task ExplicitDelivery_ConcurrentReadAndRepeatedAck_StoresOneReadReceipt()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<DeliveryDb>().UseNpgsql(postgres.GetConnectionString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options;
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var sender = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var recipient = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        var message = Message(Guid.NewGuid(), thread.Id, sender.Id, tenant, "concurrent receipt");
        var delivered = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, Name = "Delivered", SystemReferenceId = MessageDeliveryTypes.Delivered, IsEnabled = true };
        var read = new MessageDeliveryType { Id = Guid.NewGuid(), TenantId = tenant, Name = "Read", SystemReferenceId = MessageDeliveryTypes.Read, IsEnabled = true };
        await using (var seed = new DeliveryDb(options))
        {
            await seed.Database.EnsureCreatedAsync(); seed.AddRange(thread, sender, recipient, message, delivered, read);
            await seed.SaveChangesAsync();
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            await using var db = new DeliveryDb(options);
            var service = CreateService(new ServerDataContext<DeliveryDb>(db), database: db);
            return i % 2 == 0
                ? await service.MarkMessagesDeliveredAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id], Metadata = Metadata(recipient.CredentialId, tenant) })
                : await service.MarkMessagesReadAsync(new() { ThreadId = thread.Id, MessageIds = [message.Id], Metadata = Metadata(recipient.CredentialId, tenant) });
        }));
        Assert.That(results.All(r => r.IsSuccess), Is.True, string.Join("; ", results.Select(r => r.Message)));
        await using var check = new DeliveryDb(options);
        Assert.That((await check.Set<MessageDelivery>().SingleAsync()).TypeId, Is.EqualTo(read.Id));
        Assert.That(await check.Set<MessageOutboxEvent>().CountAsync(e => e.EventType == MessageRealtimeEvents.MessagesRead), Is.EqualTo(1));
        Assert.That(await check.Set<MessageOutboxEvent>().CountAsync(e => e.EventType == MessageRealtimeEvents.MessagesDelivered), Is.LessThanOrEqualTo(1));
    }

    private sealed class DeliveryDb(DbContextOptions<DeliveryDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<Message>().Ignore(x => x.MessageDeliveries).Ignore(x => x.MessageFiles).Ignore(x => x.MessageReactions)
                .Ignore(x => x.MessageThread).Ignore(x => x.MessageThreadMember).Ignore(x => x.ParentMessage).Ignore(x => x.Replies);
            model.Entity<MessageThreadMember>().Ignore(x => x.Group).Ignore(x => x.Credential).Ignore(x => x.MessageDeliveries)
                .Ignore(x => x.MessageThread).Ignore(x => x.MessageThreadMemberRoles).Ignore(x => x.Messages);
            model.Entity<MessageThread>().Ignore(x => x.Type).Ignore(x => x.MessageThreadMemberGroups).Ignore(x => x.MessageThreadMembers).Ignore(x => x.Messages);
            model.Entity<MessageDelivery>().Ignore(x => x.Message).Ignore(x => x.MessageThreadMember).Ignore(x => x.Type)
                .HasIndex(x => new { x.MessageThreadMemberId, x.MessageId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            model.Entity<MessageDeliveryType>().Ignore(x => x.MessageDeliveries);
            model.Entity<MessageHidden>(); model.Entity<MessageBlock>(); model.Entity<MessageOutboxEvent>();
            model.Entity<RegistryConfiguration>();
        }
    }
}
