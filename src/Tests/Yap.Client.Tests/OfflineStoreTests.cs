using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class OfflineStoreTests
{
    [Test]
    public async Task QueueAsync_Reload_PreservesMessageUploadAndClearsOnlyItsDraft()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message();
        await fixture.Store.SaveDraftAsync("account-a", "main", "draft");
        await fixture.Store.SaveDraftAsync("account-b", "main", "private draft");
        var queued = fixture.Queue(message);
        queued.FileKey = "opfs-file"; queued.FileName = "photo.png"; queued.FileSize = 42;
        await fixture.Store.QueueAsync(queued, message, "main");
        var reloaded = new OfflineStore(fixture);
        Assert.That((await reloaded.PendingAsync("account-a")).Single().FileKey, Is.EqualTo("opfs-file"));
        Assert.That((await reloaded.MessagesAsync("account-a", message.ThreadId)).Single().Text, Is.EqualTo(message.Text));
        Assert.That(await reloaded.DraftAsync("account-a", "main"), Is.Empty);
        Assert.That(await reloaded.DraftAsync("account-b", "main"), Is.EqualTo("private draft"));
        Assert.That(await reloaded.PendingAsync("account-b"), Is.Empty);
    }

    [Test]
    public async Task QueueAsync_DuplicateId_RollsBackAndPreservesDraft()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message();
        await fixture.Store.QueueAsync(fixture.Queue(message), message, "main");
        await fixture.Store.SaveDraftAsync("account-a", "main", "do not lose me");
        Assert.ThrowsAsync<DbUpdateException>(async () => await fixture.Store.QueueAsync(fixture.Queue(message), message, "main"));
        Assert.That(await fixture.Store.DraftAsync("account-a", "main"), Is.EqualTo("do not lose me"));
        Assert.That(await fixture.Store.PendingAsync("account-a"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReplaceWindowAsync_DeletedRemoteMessage_RemovesStaleCacheButKeepsOutbox()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var deleted = fixture.Message(); var queued = fixture.Message(deleted.ThreadId);
        await fixture.Store.SaveMessagesAsync("account-a", [deleted]);
        await fixture.Store.QueueAsync(fixture.Queue(queued), queued, "main");
        await fixture.Store.ReplaceWindowAsync("account-a", deleted.ThreadId, [], true);
        Assert.That((await fixture.Store.MessagesAsync("account-a", deleted.ThreadId)).Select(x => x.Id), Is.EqualTo(new[] { queued.Id }));
    }

    [Test]
    public async Task MessagesAsync_UploadStillPending_DoesNotDisplayPrematureSentReceipt()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message(); var queued = fixture.Queue(message);
        queued.MessageConfirmed = true; queued.FileKey = "pending-upload";
        await fixture.Store.QueueAsync(queued, message, "main");
        message.Delivery = "Sent";
        await fixture.Store.ReplaceWindowAsync("account-a", message.ThreadId, [message], true);
        Assert.That((await fixture.Store.MessagesAsync("account-a", message.ThreadId)).Single().Delivery, Is.EqualTo("Queued"));
        queued.Paused = true;
        await fixture.Store.SaveQueueAsync(queued);
        Assert.That((await fixture.Store.MessagesAsync("account-a", message.ThreadId)).Single().Delivery, Does.Contain("Needs attention"));
    }

    [Test]
    public async Task CompleteQueueAsync_ConfirmedSend_UpdatesDisplayAtomically()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message(); var queued = fixture.Queue(message);
        await fixture.Store.QueueAsync(queued, message, "main");
        await fixture.Store.CompleteQueueAsync(queued);
        Assert.That(await fixture.Store.PendingAsync("account-a"), Is.Empty);
        Assert.That((await fixture.Store.MessagesAsync("account-a", message.ThreadId)).Single().Delivery, Is.EqualTo("Sent"));
    }

    [Test]
    public async Task ClearPrivateAsync_OfflineLogout_PreservesLogoutIntentAndRemovesAllAccounts()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message();
        await fixture.Store.QueueAsync(fixture.Queue(message), message, "main");
        await fixture.Store.SetSettingAsync("user", "cached identity");
        await fixture.Store.SetSettingAsync("pendingLogout", "true");
        await fixture.Store.SaveDraftAsync("account-b", "reply", "private text");
        await fixture.Store.ClearPrivateAsync();
        Assert.That(await fixture.Store.SettingAsync("user"), Is.Null);
        Assert.That(await fixture.Store.SettingAsync("pendingLogout"), Is.EqualTo("true"));
        Assert.That(await fixture.Store.PendingAsync("account-a"), Is.Empty);
        Assert.That(await fixture.Store.MessagesAsync("account-a", message.ThreadId), Is.Empty);
        Assert.That(await fixture.Store.DraftAsync("account-b", "reply"), Is.Empty);
    }
}

internal sealed class StoreFixture(SqliteConnection connection) : IDbContextFactory<OfflineDatabase>, IAsyncDisposable
{
    public OfflineStore Store => new(this);
    public OfflineDatabase CreateDbContext() => new(new DbContextOptionsBuilder<OfflineDatabase>().UseSqlite(connection).Options);
    public static async Task<StoreFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var fixture = new StoreFixture(connection);
        await using var db = fixture.CreateDbContext(); await db.Database.EnsureCreatedAsync(); return fixture;
    }
    public ChatMessage Message(Guid? thread = null) => new() { Id = Guid.NewGuid(), ThreadId = thread ?? Guid.NewGuid(), Text = "saved offline", CreatedAt = DateTime.UtcNow, Delivery = "Queued" };
    public QueuedMessage Queue(ChatMessage message) => new() { Scope = "account-a", Id = message.Id, ThreadId = message.ThreadId, Text = message.Text, CreatedTicks = message.CreatedAt.Ticks };
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
