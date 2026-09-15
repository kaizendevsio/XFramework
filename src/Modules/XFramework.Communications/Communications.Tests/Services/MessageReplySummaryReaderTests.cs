using Communications.Api.Services;
using Communications.Domain.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Communications.Tests.Services;

public sealed class MessageReplySummaryReaderTests
{
    [Test]
    public async Task ReadAsync_CountsThreadRepliesOnly_SoTheBadgeMatchesTheThreadPage()
    {
        var tenant = Guid.NewGuid(); var thread = Guid.NewGuid(); var sender = Guid.NewGuid();
        var parent = Row(tenant, thread, sender);
        var inline = Row(tenant, thread, sender, parent.Id, threadReply: false);
        var first = Row(tenant, thread, sender, parent.Id, threadReply: true);
        var second = Row(tenant, thread, sender, parent.Id, threadReply: true);
        var deleted = Row(tenant, thread, sender, parent.Id, threadReply: true); deleted.IsDeleted = true;
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ReplyDb(new DbContextOptionsBuilder<ReplyDb>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.AddRange(parent, inline, first, second, deleted);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        var counts = await new MessageReplySummaryReader(db).ReadAsync(tenant, thread, [parent.Id], [], [], default);
        Assert.That(counts[parent.Id], Is.EqualTo(2));
        Assert.That(await new MessageReplySummaryReader(db).ReadAsync(tenant, thread, [parent.Id], [], [second.Id], default),
            Is.EqualTo(new Dictionary<Guid, int> { [parent.Id] = 1 }));
    }

    private static Message Row(Guid tenant, Guid thread, Guid member, Guid? parent = null, bool threadReply = false) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, MessageThreadId = thread, MessageThreadMemberId = member,
        ParentMessageId = parent, IsThreadReply = threadReply, IsEnabled = true
    };

    private sealed class ReplyDb(DbContextOptions<ReplyDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            var message = model.Entity<Message>();
            HashSet<string> columns = ["Id", "TenantId", "MessageThreadId", "MessageThreadMemberId", "IsEnabled", "IsDeleted", "ParentMessageId", "IsThreadReply"];
            foreach (var property in typeof(Message).GetProperties().Where(x => !columns.Contains(x.Name))) message.Ignore(property.Name);
            message.HasKey(x => x.Id);
        }
    }
}
