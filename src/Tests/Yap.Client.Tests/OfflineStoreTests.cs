using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class OfflineStoreTests
{
    [Test]
    public async Task EncryptionReset_ClearsAccountHistoryAndOutbox_ButKeepsLoginFavoritesAndOtherAccounts()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message();
        await fixture.Store.SetSettingAsync("user", "signed-in-account");
        await fixture.Store.SaveConversationsAsync("account-a", [new() { Id = message.ThreadId, IsFavorite = true, LastMessage = message, Preview = message.Text }]);
        await fixture.Store.QueueAsync(fixture.Queue(message), message, "draft");
        await fixture.Store.SaveMessagesAsync("account-b", [message]);
        await fixture.Store.ClearEncryptionHistoryAsync("account-a");
        Assert.That(await fixture.Store.PendingAsync("account-a"), Is.Empty);
        Assert.That(await fixture.Store.MessagesAsync("account-a", message.ThreadId), Is.Empty);
        Assert.That(await fixture.Store.MessagesAsync("account-b", message.ThreadId), Has.Count.EqualTo(1));
        Assert.That(await fixture.Store.SettingAsync("user"), Is.EqualTo("signed-in-account"));
        var conversation = (await fixture.Store.ConversationsAsync("account-a")).Single();
        Assert.That(conversation.IsFavorite, Is.True);
        Assert.That(conversation.LastMessage, Is.Null);
        Assert.That(conversation.Preview, Is.EqualTo("Start a conversation"));
    }

    [Test]
    public async Task CompleteEncryptedUpload_PreservesVerifiedMetadataForSenderPreview()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message();
        message.EncryptedEnvelope = "opaque signed envelope";
        message.LocalFileKey = "local-upload";
        message.HasAttachments = true;
        var attachment = new ChatAttachment(Guid.NewGuid(), "photo.jpg", "image/jpeg", 7000000, Guid.NewGuid(), 3);
        message.Attachments = [attachment];
        var queued = new QueuedMessage { Scope = "a", Id = message.Id, ThreadId = message.ThreadId, FileKey = message.LocalFileKey };
        await fixture.Store.QueueAsync(queued, message, "draft");
        await fixture.Store.CompleteQueueAsync(queued);
        var saved = await fixture.Store.MessageAsync("a", message.Id);
        Assert.That(saved!.Attachments, Is.EqualTo(new[] { attachment }));
        Assert.That(saved.LocalFileKey, Is.Null);
        Assert.That(saved.Delivery, Is.EqualTo("Sent"));
        Assert.That(await fixture.Store.PendingAsync("a"), Is.Empty);
    }

    [Test]
    public async Task HistoryWindow_AnchorsAcrossNewArrivals_AndKeepsEqualTimestampOrder()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var thread = Guid.NewGuid(); var now = DateTime.UtcNow.Date;
        var messages = Enumerable.Range(0, 250).Select(i => { var m = fixture.Message(thread); m.CreatedAt = now.AddSeconds(i / 2); return m; }).ToList();
        await fixture.Store.SaveMessagesAsync("a", messages);
        var window = await fixture.Store.MessagesAsync("a", thread, limit: 100, skip: 50);
        Assert.That(window, Has.Count.EqualTo(100));
        Assert.That(await fixture.Store.MessageOffsetAsync("a", thread, window[^1].Id), Is.EqualTo(50));
        var arrival = fixture.Message(thread); arrival.CreatedAt = now.AddDays(1);
        await fixture.Store.SaveMessagesAsync("a", [arrival]);
        var offset = await fixture.Store.MessageOffsetAsync("a", thread, window[^1].Id);
        Assert.That(offset, Is.EqualTo(51));
        var refreshed = await fixture.Store.MessagesAsync("a", thread, limit: 100, skip: offset);
        Assert.That(refreshed.Select(x => x.Id), Is.EqualTo(window.Select(x => x.Id)));
        Assert.That(await fixture.Store.MessageOffsetAsync("b", thread, window[^1].Id), Is.Zero);
    }

    [Test]
    public async Task Favorite_SurvivesServerRefresh_AndStaysWithinAccount()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var id = Guid.NewGuid();
        await fixture.Store.SaveConversationsAsync("a", [new() { Id = id }]);
        await fixture.Store.SaveConversationsAsync("b", [new() { Id = id }]);
        await fixture.Store.SetFavoriteAsync("a", id, true);
        await fixture.Store.SaveConversationsAsync("a", [new() { Id = id, Name = "Updated" }]);
        Assert.That((await fixture.Store.ConversationsAsync("a")).Single().IsFavorite, Is.True);
        Assert.That((await fixture.Store.ConversationsAsync("b")).Single().IsFavorite, Is.False);
        await fixture.Store.SetFavoriteAsync("a", id, false);
        Assert.That((await fixture.Store.ConversationsAsync("a")).Single().IsFavorite, Is.False);
    }

    [Test]
    public async Task RemoveConversation_DeletesOnlyItsLocalHistoryAndDrafts_AndArchiveRefreshKeepsItHidden()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message(); var other = fixture.Message();
        var draft = message.ThreadId.ToString("N") + ":main";
        await fixture.Store.SaveConversationsAsync("account-a", [new() { Id = message.ThreadId }, new() { Id = other.ThreadId }]);
        await fixture.Store.SaveConversationsAsync("account-b", [new() { Id = message.ThreadId }]);
        await fixture.Store.SaveMessagesAsync("account-a", [message, other]);
        await fixture.Store.SaveMessagesAsync("account-b", [message]);
        await fixture.Store.SaveDraftAsync("account-a", draft, "local draft");
        await fixture.Store.SaveDraftAsync("account-b", draft, "other account draft");
        await fixture.Store.RemoveConversationAsync("account-a", message.ThreadId);
        await fixture.Store.SaveConversationsAsync("account-a", [new() { Id = message.ThreadId, Removed = true }]);
        Assert.That((await fixture.Store.ConversationsAsync("account-a")).Single().Id, Is.EqualTo(other.ThreadId));
        Assert.That(await fixture.Store.MessagesAsync("account-a", message.ThreadId), Is.Empty);
        Assert.That(await fixture.Store.MessagesAsync("account-a", other.ThreadId), Has.Count.EqualTo(1));
        Assert.That(await fixture.Store.MessagesAsync("account-b", message.ThreadId), Has.Count.EqualTo(1));
        Assert.That(await fixture.Store.DraftAsync("account-a", draft), Is.Empty);
        Assert.That(await fixture.Store.DraftAsync("account-b", draft), Is.EqualTo("other account draft"));
        await fixture.Store.SaveConversationsAsync("account-a", [new() { Id = message.ThreadId }]);
        Assert.That(await fixture.Store.ConversationsAsync("account-a"), Has.Count.EqualTo(2), "Explicitly reopening the chat restores its list entry");
    }

    [Test]
    public async Task MessagesAsync_LimitsLatestWindowWithoutLosingOlderOfflineHistory()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var thread = Guid.NewGuid();
        var messages = Enumerable.Range(0, 250).Select(i => { var m = fixture.Message(thread); m.Text = $"Message {i}"; m.CreatedAt = DateTime.UtcNow.Date.AddSeconds(i); return m; }).ToList();
        await fixture.Store.SaveMessagesAsync("account-a", messages);
        var window = await fixture.Store.MessagesAsync("account-a", thread, limit: 50);
        Assert.That(window.Count, Is.EqualTo(50));
        Assert.That(window[0].Text, Is.EqualTo("Message 200"));
        Assert.That(window[^1].Text, Is.EqualTo("Message 249"));
        Assert.That(await fixture.Store.MessageCountAsync("account-a", thread), Is.EqualTo(250));
    }

    [Test]
    public async Task SummaryRefresh_PreservesConversationControls_AndPendingMediaKeepsPreview()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var message = fixture.Message(); message.LocalFileKey = "photo"; message.HasAttachments = true;
        message.Attachments = [new(message.Id, "photo.png", "image/png", 42)]; message.IsThreadReply = true;
        await fixture.Store.QueueAsync(fixture.Queue(message), message, "main");
        var server = fixture.Message(message.ThreadId); server.Id = message.Id;
        await fixture.Store.ReplaceWindowAsync("account-a", message.ThreadId, [server], true);
        Assert.That((await fixture.Store.MessageAsync("account-a", message.Id))!.LocalFileKey, Is.EqualTo("photo"));
        var details = new Conversation { Id = message.ThreadId, Features = 3, CanManage = true, People = [new(Guid.NewGuid(), "Admin", "admin", Role: "Admin")] };
        await fixture.Store.SaveConversationsAsync("account-a", [details]);
        await fixture.Store.SaveConversationsAsync("account-a", [new() { Id = details.Id, Preview = "New message" }]);
        var cached = (await fixture.Store.ConversationsAsync("account-a")).Single();
        Assert.That(cached.Features, Is.EqualTo(3));
        Assert.That(cached.CanManage, Is.True);
        Assert.That(cached.People.Single().Role, Is.EqualTo("Admin"));
    }

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

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReplaceEncryptedWindow_PreservesPendingBody_OrFreshVerifiedAttachmentMetadata(bool pending)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var old = fixture.Message(); old.EncryptedEnvelope = "old signed envelope"; old.HasAttachments = true;
        old.LocalFileKey = pending ? "old-local-file" : null;
        old.Attachments = [new(Guid.NewGuid(), "old.png", "image/png", 42, Guid.NewGuid(), 1)];
        if (pending) await fixture.Store.QueueAsync(fixture.Queue(old), old, "main");
        else await fixture.Store.SaveMessagesAsync("account-a", [old]);
        var verified = fixture.Message(old.ThreadId); verified.Id = old.Id;
        verified.EncryptedEnvelope = "new signed envelope"; verified.HasAttachments = true;
        var replacement = new ChatAttachment(Guid.NewGuid(), "new.png", "image/png", 100, Guid.NewGuid(), 2);
        verified.Attachments = [replacement];
        await fixture.Store.ReplaceWindowAsync("account-a", old.ThreadId, [verified], true);
        var saved = (await fixture.Store.MessageAsync("account-a", old.Id))!;
        Assert.That(saved.EncryptedEnvelope, Is.EqualTo(pending ? old.EncryptedEnvelope : verified.EncryptedEnvelope));
        Assert.That(saved.Attachments.Single(), Is.EqualTo(pending ? old.Attachments[0] : replacement));
        Assert.That(saved.LocalFileKey, Is.EqualTo(pending ? old.LocalFileKey : null));
    }

    [TestCase(null)]
    [TestCase("untrusted replacement envelope")]
    public async Task ReplaceWindow_PendingMessageCannotBeRewrittenByRemoteRefresh(string? remoteEnvelope)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var local = fixture.Message(); local.Text = "What the user wrote"; local.EncryptedEnvelope = "persisted randomized ciphertext";
        local.EncryptionSenderDeviceId = Guid.NewGuid(); local.AcceptedSenderDirectoryRevision = 3;
        await fixture.Store.QueueAsync(fixture.Queue(local), local, "main");
        var remote = fixture.Message(local.ThreadId); remote.Id = local.Id; remote.Text = "Text the user never wrote";
        remote.EncryptedEnvelope = remoteEnvelope;
        remote.EncryptionSenderDeviceId = Guid.NewGuid(); remote.AcceptedSenderDirectoryRevision = 9;
        await fixture.Store.ReplaceWindowAsync("account-a", local.ThreadId, [remote], true);
        var saved = (await fixture.Store.MessageAsync("account-a", local.Id))!;
        Assert.That(saved.Text, Is.EqualTo(local.Text));
        Assert.That(saved.EncryptedEnvelope, Is.EqualTo(local.EncryptedEnvelope));
        Assert.That(saved.EncryptionSenderDeviceId, Is.EqualTo(local.EncryptionSenderDeviceId));
        Assert.That(saved.AcceptedSenderDirectoryRevision, Is.EqualTo(local.AcceptedSenderDirectoryRevision));
        Assert.That((await fixture.Store.PendingAsync("account-a")).Single().Text, Is.EqualTo(local.Text));
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

public sealed class OfflineSchemaTests
{
    [Test]
    public async Task UpgradeAsync_DeviceFromAnEarlierBuild_GainsTheStreamedUploadColumn()
    {
        // EnsureCreated leaves an existing device database untouched, so a device that
        // installed Yap before streamed uploads would otherwise fail on every outbox read.
        await using var fixture = await StoreFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Outbox\" DROP COLUMN \"UploadId\"");

        await OfflineDatabase.UpgradeAsync(db);
        await OfflineDatabase.UpgradeAsync(db);

        var message = fixture.Message();
        var queued = fixture.Queue(message);
        queued.UploadId = Guid.NewGuid();
        await fixture.Store.QueueAsync(queued, message, "main");

        Assert.That((await fixture.Store.PendingAsync("account-a")).Single().UploadId, Is.EqualTo(queued.UploadId));
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
