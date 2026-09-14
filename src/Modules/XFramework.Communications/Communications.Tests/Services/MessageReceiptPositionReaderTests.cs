using Communications.Api.Services;
using Communications.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Communications.Tests.Services;

[TestFixture, Category("Kind:Integration")]
public sealed class MessageReceiptPositionReaderTests
{
    [Test]
    public async Task Positions_ExecuteInPostgres_RespectScopeVisibilityAndThreadReplies()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await using var db = new ReceiptDb(new DbContextOptionsBuilder<ReceiptDb>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.EnsureCreatedAsync();
        var tenant = Guid.NewGuid(); var thread = Guid.NewGuid(); var sender = Guid.NewGuid();
        var reader = Guid.NewGuid(); var readType = Guid.NewGuid();
        var messages = Enumerable.Range(0, 120).Select(i => new Message {
            Id = Guid.NewGuid(), TenantId = tenant, MessageThreadId = thread, MessageThreadMemberId = sender,
            CreatedAt = DateTime.UtcNow.Date.AddMinutes(i), IsEnabled = true
        }).ToArray();
        messages[119].IsDeleted = true; messages[118].TenantId = Guid.NewGuid();
        messages[117].IsThreadReply = true; messages[117].ParentMessageId = messages[0].Id;
        db.AddRange(messages);
        db.AddRange(messages.Select(message => new MessageDelivery { Id = Guid.NewGuid(), TenantId = tenant,
            MessageId = message.Id, MessageThreadMemberId = reader, TypeId = readType, IsEnabled = true }));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var positions = new MessageReceiptPositionReader(db);
        var result = await positions.ReadAsync(tenant, thread, sender, null, [messages[116].Id], [reader], readType, default);
        Assert.That(result.LatestOwnId, Is.EqualTo(messages[115].Id));
        Assert.That(result.LastReadByMember, Has.Count.EqualTo(1));
        Assert.That(result.LastReadByMember[reader], Is.EqualTo(messages[115].Id));
        var reply = await positions.ReadAsync(tenant, thread, sender, messages[0].Id, [], [reader], readType, default);
        Assert.That(reply.LatestOwnId, Is.EqualTo(messages[117].Id));
        Assert.That(reply.LastReadByMember[reader], Is.EqualTo(messages[117].Id));
        Assert.That((await positions.ReadAsync(Guid.NewGuid(), thread, sender, null, [], [reader], readType, default)).LatestOwnId, Is.Null);
        Assert.That((await positions.ReadAsync(tenant, thread, sender, null, [], [reader], null, default)).LastReadByMember, Is.Empty);
        Assert.That(db.ChangeTracker.Entries(), Is.Empty);
    }

    private sealed class ReceiptDb(DbContextOptions<ReceiptDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            var message = model.Entity<Message>();
            HashSet<string> messageColumns = ["Id", "TenantId", "MessageThreadId", "MessageThreadMemberId", "CreatedAt", "IsEnabled", "IsDeleted", "ParentMessageId", "IsThreadReply"];
            foreach (var property in typeof(Message).GetProperties().Where(x => !messageColumns.Contains(x.Name))) message.Ignore(property.Name);
            message.HasKey(x => x.Id);
            var delivery = model.Entity<MessageDelivery>();
            HashSet<string> deliveryColumns = ["Id", "TenantId", "MessageId", "MessageThreadMemberId", "TypeId", "IsEnabled", "IsDeleted"];
            foreach (var property in typeof(MessageDelivery).GetProperties().Where(x => !deliveryColumns.Contains(x.Name))) delivery.Ignore(property.Name);
            delivery.HasKey(x => x.Id);
        }
    }
}
