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
            if (row is not null && conversation.People.Count == 0)
                conversation.People = JsonSerializer.Deserialize<Conversation>(row.Json, Json)!.People;
            var json = JsonSerializer.Serialize(conversation, Json);
            if (row is null) db.Conversations.Add(new() { Scope = scope, Id = conversation.Id, Json = json });
            else row.Json = json;
        }
        return await db.SaveChangesAsync(ct);
    }, ct);

    public Task<List<Conversation>> ConversationsAsync(string scope, CancellationToken ct = default) => UseAsync(async db =>
        (await db.Conversations.AsNoTracking().Where(x => x.Scope == scope).ToListAsync(ct))
        .Select(x => JsonSerializer.Deserialize<Conversation>(x.Json, Json)!).ToList(), ct);

    public Task SaveMessagesAsync(string scope, IEnumerable<ChatMessage> messages, CancellationToken ct = default) => UseAsync(async db =>
    {
        foreach (var message in messages) await UpsertMessageAsync(db, scope, message, ct);
        return await db.SaveChangesAsync(ct);
    }, ct);

    public Task<List<ChatMessage>> MessagesAsync(string scope, Guid thread, CancellationToken ct = default) => UseAsync(async db =>
    {
        var messages = (await db.Messages.AsNoTracking().Where(x => x.Scope == scope && x.ThreadId == thread).OrderBy(x => x.CreatedTicks).ToListAsync(ct))
            .Select(x => JsonSerializer.Deserialize<ChatMessage>(x.Json, Json)!).ToList();
        var pending = await db.Outbox.AsNoTracking().Where(x => x.Scope == scope && x.ThreadId == thread).ToDictionaryAsync(x => x.Id, ct);
        foreach (var message in messages)
            if (pending.TryGetValue(message.Id, out var item)) message.Delivery = item.Paused ? "Needs attention · retry" : "Queued";
        return messages;
    }, ct);

    public Task ReplaceWindowAsync(string scope, Guid thread, List<ChatMessage> messages, bool complete, CancellationToken ct = default) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ids = messages.Select(x => x.Id).ToList();
        var pending = await db.Outbox.Where(x => x.Scope == scope).Select(x => x.Id).ToListAsync(ct);
        var oldest = messages.Count == 0 ? long.MaxValue : messages.Min(x => x.CreatedAt.Ticks);
        await db.Messages.Where(x => x.Scope == scope && x.ThreadId == thread && !pending.Contains(x.Id) && !ids.Contains(x.Id)
            && (complete || x.CreatedTicks >= oldest)).ExecuteDeleteAsync(ct);
        foreach (var message in messages)
        {
            var existing = await db.Messages.FindAsync([scope, message.Id], ct);
            // Preserve already opened attachment metadata when the message listing omits files.
            if (existing is not null) message.Attachments = JsonSerializer.Deserialize<ChatMessage>(existing.Json, Json)!.Attachments;
            await UpsertMessageAsync(db, scope, message, ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }, ct);

    public Task QueueAsync(QueuedMessage item, ChatMessage message, string draftKey, CancellationToken ct = default) => UseAsync(async db =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Outbox.Add(item);
        await UpsertMessageAsync(db, item.Scope, message, ct);
        var draft = await db.Drafts.FindAsync([item.Scope, draftKey], ct);
        if (draft is not null) db.Drafts.Remove(draft);
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
            message.Delivery = "Sent";
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
        row.Json = JsonSerializer.Serialize(message, Json);
    }
}
