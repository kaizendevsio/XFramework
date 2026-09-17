using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed class OfflineStore(IDbContextFactory<OfflineDatabase> factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    public static string Scope(UserSession user) => $"{user.TenantId:N}:{user.CredentialId:N}";

    // SQLite transactions serialize related outbox/cache writes. One gate prevents
    // overlapping operations on the browser provider's shared worker connection.
    public async Task<T> UseAsync<T>(Func<OfflineDatabase, Task<T>> action, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { await using var db = await factory.CreateDbContextAsync(ct); return await action(db); }
        finally { gate.Release(); }
    }

    public Task<string?> SettingAsync(string key, CancellationToken ct = default) => UseAsync(async db =>
        (await db.Settings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct))?.Value, ct);

    public Task SetSettingAsync(string key, string value, CancellationToken ct = default) => UseAsync(async db =>
    {
        var row = await db.Settings.FindAsync([key], ct);
        if (row is null) db.Settings.Add(new() { Key = key, Value = value }); else row.Value = value;
        return await db.SaveChangesAsync(ct);
    }, ct);

    public Task SaveConversationsAsync(string scope, IEnumerable<Conversation> conversations, CancellationToken ct = default) => UseAsync(async db =>
    {
        foreach (var conversation in conversations)
        {
            var row = await db.Conversations.FindAsync([scope, conversation.Id], ct);
            if (conversation.Removed)
            {
                if (row is not null) db.Conversations.Remove(row);
                continue;
            }
            if (row is not null)
                conversation.IsFavorite = JsonSerializer.Deserialize<Conversation>(row.Json, Json)!.IsFavorite;
            if (row is not null && conversation.People.Count == 0)
            {
                var saved = JsonSerializer.Deserialize<Conversation>(row.Json, Json)!;
                conversation.People = saved.People; conversation.Features = saved.Features;
                conversation.CanManage = saved.CanManage;
                conversation.MessageTotal = Math.Max(conversation.MessageTotal, saved.MessageTotal);
            }
            var json = JsonSerializer.Serialize(conversation, Json);
            if (row is null) db.Conversations.Add(new() { Scope = scope, Id = conversation.Id, Json = json });
            else row.Json = json;
        }
        return await db.SaveChangesAsync(ct);
    }, ct);

    public Task SetFavoriteAsync(string scope, Guid id, bool favorite) => UseAsync(async db =>
    {
        var row = await db.Conversations.FindAsync([scope, id]);
        if (row is null) return 0;
        var conversation = JsonSerializer.Deserialize<Conversation>(row.Json, Json)!;
        conversation.IsFavorite = favorite;
        row.Json = JsonSerializer.Serialize(conversation, Json);
        return await db.SaveChangesAsync();
    });

    public Task<List<Conversation>> ConversationsAsync(string scope, CancellationToken ct = default) => UseAsync(async db =>
        (await db.Conversations.AsNoTracking().Where(x => x.Scope == scope).ToListAsync(ct))
        .Select(x => JsonSerializer.Deserialize<Conversation>(x.Json, Json)!).ToList(), ct);

    public Task SaveMessagesAsync(string scope, IEnumerable<ChatMessage> messages, CancellationToken ct = default) => UseAsync(async db =>
    {
        foreach (var message in messages) await UpsertMessageAsync(db, scope, message, ct);
        return await db.SaveChangesAsync(ct);
    }, ct);

    public Task RemoveConversationAsync(string scope, Guid thread, CancellationToken ct = default, bool discardPending = false) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Conversations.Where(x => x.Scope == scope && x.Id == thread).ExecuteDeleteAsync(ct);
        await db.Messages.Where(x => x.Scope == scope && x.ThreadId == thread).ExecuteDeleteAsync(ct);
        if (discardPending) await db.Outbox.Where(x => x.Scope == scope && x.ThreadId == thread).ExecuteDeleteAsync(ct);
        var prefix = thread.ToString("N") + ":";
        await db.Drafts.Where(x => x.Scope == scope && x.Key.StartsWith(prefix)).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }, ct);

    // Mirrors the server: the timeline never counts thread replies, a parent counts only its own.
    public Task<int> MessageCountAsync(string scope, Guid thread) => UseAsync(db => db.Messages.CountAsync(x => x.Scope == scope && x.ThreadId == thread && !x.IsThreadReply));
    public Task<int> ReplyCountAsync(string scope, Guid parent) => UseAsync(db => db.Messages.CountAsync(x => x.Scope == scope && x.ParentId == parent && x.IsThreadReply));

    // Bookmarks are read back from the device so the saved list still opens with no connection.
    public Task<List<ChatMessage>> SavedMessagesAsync(string scope, int limit = 0, int skip = 0, CancellationToken ct = default) => UseAsync(async db =>
    {
        var query = db.Messages.AsNoTracking().Where(x => x.Scope == scope && x.Saved)
            .OrderByDescending(x => x.CreatedTicks).ThenByDescending(x => x.Id).AsQueryable();
        if (skip > 0) query = query.Skip(skip);
        if (limit > 0) query = query.Take(limit);
        return (await query.ToListAsync(ct)).Select(x => JsonSerializer.Deserialize<ChatMessage>(x.Json, Json)!).ToList();
    }, ct);

    public Task<int> SavedMessageCountAsync(string scope, CancellationToken ct = default) =>
        UseAsync(db => db.Messages.CountAsync(x => x.Scope == scope && x.Saved, ct), ct);

    public Task SetSavedAsync(string scope, Guid id, bool saved, CancellationToken ct = default) => UseAsync(async db =>
    {
        var row = await db.Messages.FindAsync([scope, id], ct);
        if (row is null) return 0;
        var message = JsonSerializer.Deserialize<ChatMessage>(row.Json, Json)!;
        message.Saved = saved;
        row.Saved = saved;
        row.Json = JsonSerializer.Serialize(message, Json);
        return await db.SaveChangesAsync(ct);
    }, ct);

    public Task<ChatMessage?> MessageAsync(string scope, Guid id) => UseAsync(async db =>
    {
        var row = await db.Messages.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.Id == id);
        return row is null ? null : JsonSerializer.Deserialize<ChatMessage>(row.Json, Json);
    });

    public Task<int> MessageOffsetAsync(string scope, Guid thread, Guid id, Guid? parent = null) => UseAsync(async db =>
    {
        var row = await db.Messages.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.ThreadId == thread && x.Id == id);
        if (row is null) return 0;
        if (parent.HasValue)
            return await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Messages WHERE Scope = {scope} AND ThreadId = {thread} AND ParentId = {parent.Value} AND IsThreadReply = 1 AND (CreatedTicks > {row.CreatedTicks} OR (CreatedTicks = {row.CreatedTicks} AND Id > {id}))").SingleAsync();
        return await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Messages WHERE Scope = {scope} AND ThreadId = {thread} AND IsThreadReply = 0 AND (CreatedTicks > {row.CreatedTicks} OR (CreatedTicks = {row.CreatedTicks} AND Id > {id}))").SingleAsync();
    });

    public Task<List<ChatMessage>> MessageUpdatesAsync(string scope, List<Guid> ids) => UseAsync(async db =>
        (await db.Messages.AsNoTracking().Where(x => x.Scope == scope && ids.Contains(x.Id)).ToListAsync())
        .Select(x => JsonSerializer.Deserialize<ChatMessage>(x.Json, Json)!).ToList());

    public Task<List<ChatMessage>> MessagesAsync(string scope, Guid thread, CancellationToken ct = default, int limit = 0, int skip = 0, Guid? parent = null) => UseAsync(async db =>
    {
        var query = db.Messages.AsNoTracking().Where(x => x.Scope == scope && x.ThreadId == thread).OrderByDescending(x => x.CreatedTicks).ThenByDescending(x => x.Id).AsQueryable();
        query = parent.HasValue ? query.Where(x => x.ParentId == parent && x.IsThreadReply) : query.Where(x => !x.IsThreadReply);
        if (skip > 0) query = query.Skip(skip);
        if (limit > 0) query = query.Take(limit);
        var messages = (await query.ToListAsync(ct)).AsEnumerable().Reverse()
            .Select(x => JsonSerializer.Deserialize<ChatMessage>(x.Json, Json)!).ToList();
        var ids = messages.Select(x => x.Id).ToArray();
        var pending = await db.Outbox.AsNoTracking().Where(x => x.Scope == scope && x.ThreadId == thread && ids.Contains(x.Id)).Select(x => new { x.Id, x.Paused, x.Error }).ToDictionaryAsync(x => x.Id, ct);
        foreach (var message in messages)
            if (pending.TryGetValue(message.Id, out var item)) message.Delivery = item.Paused ? "Needs attention · retry"
                : item.Error == "Waiting for encrypted setup" ? "Sending · waiting for recipient setup" : "Queued";
        return messages;
    }, ct);

    public Task ReplaceWindowAsync(string scope, Guid thread, List<ChatMessage> messages, bool complete, CancellationToken ct = default) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ids = messages.Select(x => x.Id).ToList();
        var pending = await db.Outbox.Where(x => x.Scope == scope).Select(x => x.Id).ToListAsync(ct);
        var oldest = messages.Count == 0 ? long.MaxValue : messages.Min(x => x.CreatedAt.Ticks);
        // This window owns the timeline only. Thread replies arrive from their own paged fetch.
        await db.Messages.Where(x => x.Scope == scope && x.ThreadId == thread && !pending.Contains(x.Id) && !ids.Contains(x.Id)
            && !x.IsThreadReply && (complete || x.CreatedTicks >= oldest)).ExecuteDeleteAsync(ct);
        foreach (var message in messages)
        {
            // The outbox owns this exact body and persisted ciphertext until completion.
            // A remote refresh must not alter what the device signs or retries for this ID.
            if (pending.Contains(message.Id)) continue;
            var existing = await db.Messages.FindAsync([scope, message.Id], ct);
            // Preserve already opened attachment metadata when the message listing omits files.
            if (existing is not null)
            {
                var cached = JsonSerializer.Deserialize<ChatMessage>(existing.Json, Json)!;
                // Encrypted metadata has already been verified by DecryptMessagesAsync. Never
                // replace a freshly decrypted edit with attachment metadata from an older envelope.
                if (message.EncryptedEnvelope is null && cached.EncryptedEnvelope is null && cached.LocalFileKey is null && message.HasAttachments)
                    message.Attachments = cached.Attachments;
            }
            await UpsertMessageAsync(db, scope, message, ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }, ct);

    public Task QueueAsync(QueuedMessage item, ChatMessage message, string draftKey, CancellationToken ct = default, string? expectedDraft = null) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Outbox.Add(item);
        await UpsertMessageAsync(db, item.Scope, message, ct);
        var draft = await db.Drafts.FindAsync([item.Scope, draftKey], ct);
        if (draft is not null && (expectedDraft is null || draft.Text == expectedDraft)) db.Drafts.Remove(draft);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }, ct);

    public Task<List<QueuedMessage>> PendingAsync(string scope, CancellationToken ct = default) => UseAsync(db =>
        db.Outbox.AsNoTracking().Where(x => x.Scope == scope).OrderBy(x => x.CreatedTicks).ToListAsync(ct), ct);

    public Task SaveQueueAsync(QueuedMessage item, CancellationToken ct = default) => UseAsync(async db =>
    { db.Outbox.Update(item); return await db.SaveChangesAsync(ct); }, ct);

    public Task CompleteQueueAsync(QueuedMessage item, CancellationToken ct = default) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var cached = await db.Messages.FindAsync([item.Scope, item.Id], ct);
        if (cached is not null)
        {
            var message = JsonSerializer.Deserialize<ChatMessage>(cached.Json, Json)!;
            message.Delivery = "Sent"; message.LocalFileKey = null;
            // Encrypted file metadata lives only in the signed envelope/local cache.
            // The server's attachment list contains opaque container names and types.
            if (message.EncryptedEnvelope is null) message.Attachments = [];
            cached.Json = JsonSerializer.Serialize(message, Json);
        }
        db.Outbox.Remove(item);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }, ct);

    public Task<string> DraftAsync(string scope, string key, CancellationToken ct = default) => UseAsync(async db =>
        (await db.Drafts.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.Key == key, ct))?.Text ?? "", ct);

    public Task SaveDraftAsync(string scope, string key, string text, CancellationToken ct = default) => UseAsync(async db =>
    {
        var draft = await db.Drafts.FindAsync([scope, key], ct);
        if (draft is null) db.Drafts.Add(new() { Scope = scope, Key = key, Text = text }); else draft.Text = text;
        return await db.SaveChangesAsync(ct);
    }, ct);

    public Task<string[]> EncryptionResetFilesAsync(string scope) => UseAsync(async db =>
        (await db.Outbox.Where(x => x.Scope == scope && x.FileKey != null).Select(x => x.FileKey!).ToListAsync())
        .Concat(await db.Attachments.Where(x => x.Scope == scope).Select(x => x.FileKey).ToListAsync()).Distinct().ToArray());

    public Task ClearEncryptionHistoryAsync(string scope) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Outbox.Where(x => x.Scope == scope).ExecuteDeleteAsync();
        await db.Messages.Where(x => x.Scope == scope).ExecuteDeleteAsync();
        foreach (var row in await db.Conversations.Where(x => x.Scope == scope).ToListAsync())
        {
            var conversation = JsonSerializer.Deserialize<Conversation>(row.Json, Json)!;
            conversation.LastMessage = null; conversation.Preview = "Start a conversation";
            conversation.LastMessageAt = null; conversation.MessageTotal = 0;
            row.Json = JsonSerializer.Serialize(conversation, Json);
        }
        await db.Drafts.Where(x => x.Scope == scope).ExecuteDeleteAsync();
        await db.Attachments.Where(x => x.Scope == scope).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
        await transaction.CommitAsync(); return true;
    });

    public Task ClearPrivateAsync(CancellationToken ct = default) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Outbox.ExecuteDeleteAsync(ct);
        await db.Messages.ExecuteDeleteAsync(ct);
        await db.Conversations.ExecuteDeleteAsync(ct);
        await db.Drafts.ExecuteDeleteAsync(ct);
        await db.Attachments.ExecuteDeleteAsync(ct);
        await db.Settings.Where(x => x.Key == "user").ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }, ct);

    private static async Task UpsertMessageAsync(OfflineDatabase db, string scope, ChatMessage message, CancellationToken ct)
    {
        var row = await db.Messages.FindAsync([scope, message.Id], ct);
        if (row is null)
        {
            row = new() { Scope = scope, Id = message.Id, ThreadId = message.ThreadId, ParentId = message.ParentId, CreatedTicks = message.CreatedAt.Ticks };
            db.Messages.Add(row);
        }
        row.CreatedTicks = message.CreatedAt.Ticks;
        row.ParentId = message.ParentId;
        row.IsThreadReply = message.IsThreadReply;
        row.Saved = message.Saved;
        row.Json = JsonSerializer.Serialize(message, Json);
    }
}
