using System.Text.Json;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    // Receipts owed to senders. "Delivered" means this device decrypted and stored the
    // message, so every entry here is already readable locally; none of it is a claim
    // made on the server's behalf. All three maps are persisted together: a reload or a
    // crash between receiving a message and acknowledging it must not lose the receipt.
    private readonly Dictionary<Guid, HashSet<Guid>> deliveredPending = [];
    // Presence means a catch-up read is owed; an empty set means "sweep the newest page".
    private readonly Dictionary<Guid, HashSet<Guid>> deliveredDeferred = [];
    private readonly Dictionary<Guid, (Guid Id, DateTime At)> deliveredThrough = [];
    private string? deliveredScope;
    private Task? delivering;
    // One page per thread and only a few threads per sync: catching up across the whole
    // inbox must never become a fetch per conversation.
    private const int DeliveredSweepLimit = 4;

    private string DeliveredKey => $"delivered:{Scope}";

    private async Task LoadDeliveredAsync()
    {
        if (deliveredScope == Scope || User is null) return;
        deliveredPending.Clear(); deliveredDeferred.Clear(); deliveredThrough.Clear();
        deliveredScope = Scope;
        try
        {
            if (await store.SettingAsync(DeliveredKey, lifetime.Token) is not { Length: > 0 } saved) return;
            foreach (var item in JsonSerializer.Deserialize<List<DeliveredReceipt>>(saved, SocketJson) ?? [])
            {
                if (item.Pending.Count > 0) deliveredPending[item.Thread] = [.. item.Pending];
                if (item.Deferred is not null) deliveredDeferred[item.Thread] = [.. item.Deferred];
                if (item.Mark != Guid.Empty) deliveredThrough[item.Thread] = (item.Mark, item.MarkAt);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await RecordErrorAsync(ex); } // A corrupt snapshot only costs a re-derive.
        RetryDelivered();
    }

    private async Task SaveDeliveredAsync()
    {
        if (deliveredScope != Scope || User is null) return;
        var threads = deliveredPending.Keys.Concat(deliveredDeferred.Keys).Concat(deliveredThrough.Keys).Distinct();
        var items = threads.Select(id => new DeliveredReceipt(id,
            deliveredPending.TryGetValue(id, out var pending) ? [.. pending] : [],
            deliveredDeferred.TryGetValue(id, out var deferred) ? [.. deferred] : null,
            deliveredThrough.TryGetValue(id, out var mark) ? mark.Id : Guid.Empty,
            mark.At)).ToList();
        try { await store.SetSettingAsync(DeliveredKey, JsonSerializer.Serialize(items, SocketJson), lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await RecordErrorAsync(ex); } // Storage pressure must not stop acknowledging.
    }

    private void ForgetDelivered(Guid thread)
    {
        deliveredPending.Remove(thread); deliveredDeferred.Remove(thread); deliveredThrough.Remove(thread);
    }

    /// <summary>Records a receipt for messages this device has decrypted and stored.</summary>
    private void QueueDelivered(Guid thread, IEnumerable<ChatMessage> messages)
    {
        // The encryption gate is the definition of delivered: a message this device
        // cannot read has not arrived, and saying otherwise would lie to the sender.
        var received = messages.Where(m => !m.Mine && !m.EncryptionLocked && !m.EncryptionPending).ToList();
        if (received.Count == 0) return;
        if (!deliveredPending.TryGetValue(thread, out var pending)) deliveredPending[thread] = pending = [];
        pending.UnionWith(received.Select(m => m.Id));
        // The watermark only ever moves over messages that passed the gate, so a thread
        // whose newest message is still locked keeps being re-examined on later syncs.
        var newest = received.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Last();
        if (!deliveredThrough.TryGetValue(thread, out var mark) || newest.CreatedAt >= mark.At)
            deliveredThrough[thread] = (newest.Id, newest.CreatedAt);
        RetryDelivered();
    }

    /// <summary>Notes that a thread carries messages this device has not read yet.</summary>
    private void DeferDelivered(Guid thread, IEnumerable<Guid>? ids = null)
    {
        if (!deliveredDeferred.TryGetValue(thread, out var deferred)) deliveredDeferred[thread] = deferred = [];
        if (ids is not null) deferred.UnionWith(ids);
        if (deferred.Count > 50) deliveredDeferred[thread] = []; // Past a page, sweep instead of listing IDs.
    }

    private void RetryDelivered()
    {
        if (deliveredPending.Count > 0 && delivering is not { IsCompleted: false }) delivering = DeliverPendingAsync(Scope);
    }

    private async Task DeliverPendingAsync(string scope)
    {
        try
        {
            // Coalesce acknowledgments without delaying the next incoming message.
            await Task.Delay(30, lifetime.Token);
            // Persist before the first request: an offline or interrupted flush must
            // leave the receipts on the device rather than in this task's memory.
            await SaveDeliveredAsync();
            while (scope == Scope && deliveredPending.Count > 0)
            {
                var entry = deliveredPending.First();
                var ids = entry.Value.Take(50).ToList();
                try { await api.PostAsync("api/chat/delivered", new ReadMessages(entry.Key, ids), lifetime.Token); }
                catch (ChatApiException ex) when (ex.Status is 403 or 404)
                {
                    // Those messages are gone for this account. A stored receipt for them
                    // can never succeed and would block every later batch, so drop this
                    // batch alone — the rest of the thread still gets its own attempt.
                    if (scope != Scope) return;
                    entry.Value.ExceptWith(ids);
                    if (entry.Value.Count == 0) deliveredPending.Remove(entry.Key);
                    await SaveDeliveredAsync();
                    continue;
                }
                if (scope != Scope) return;
                entry.Value.ExceptWith(ids);
                if (entry.Value.Count == 0) deliveredPending.Remove(entry.Key);
                await SaveDeliveredAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await RecordErrorAsync(ex); } // Reconnect/catch-up retries the watermark quietly.
    }

    /// <summary>Acknowledges the newest message of every conversation in an inbox page.</summary>
    private async Task AcknowledgeInboxAsync(List<Conversation> conversations)
    {
        if (User is null) return;
        foreach (var conversation in conversations)
        {
            if (conversation.LastMessage is not { } last || last.Id == Guid.Empty) continue;
            if (deliveredThrough.TryGetValue(conversation.Id, out var mark) && mark.Id == last.Id) continue;
            // Nothing unread here means this device already opened and acknowledged the
            // thread; recording the watermark keeps later syncs free of any work for it.
            if (last.Mine || conversation.Unread == 0)
            { deliveredThrough[conversation.Id] = (last.Id, last.CreatedAt); continue; }
            QueueDelivered(conversation.Id, [last]);
            // The summary carries one message. Anything unread behind it needs the thread.
            if (conversation.Unread > 1) DeferDelivered(conversation.Id);
        }
        await SaveDeliveredAsync();
    }

    /// <summary>Reads the threads a catch-up still owes, a bounded few per sync.</summary>
    private async Task SweepDeliveredAsync()
    {
        if (User is null || !Online || NeedsLogin || deliveredDeferred.Count == 0) return;
        foreach (var (thread, deferred) in deliveredDeferred.Take(DeliveredSweepLimit).ToList())
        {
            deliveredDeferred.Remove(thread);
            // The open conversation has its own refresh; do not pay for it twice.
            if (Selected?.Id == thread) continue;
            var ids = deferred.Take(50).ToList();
            try
            {
                // Named IDs also reach thread replies, which the main timeline page omits.
                var result = await api.GetAsync<ChatPage<ChatMessage>>(ids.Count > 0
                    ? $"api/chat/conversations/{thread}/messages?" + string.Join('&', ids.Select(id => $"ids={id}"))
                    : $"api/chat/conversations/{thread}/messages?page=0", lifetime.Token);
                var items = result.Items.Where(m => m.ThreadId == thread).ToList();
                if (items.Count == 0) continue;
                await DecryptMessagesAsync(items);
                await messageChanges.WaitAsync(lifetime.Token);
                try
                {
                    if (Scope != deliveredScope) return;
                    await store.SaveMessagesAsync(Scope, items, lifetime.Token);
                    QueueDelivered(thread, items);
                }
                finally { messageChanges.Release(); }
            }
            // Membership or those messages are gone: the deferral is already dropped, and
            // receipts already queued for this thread answer for themselves when flushed.
            catch (ChatApiException ex) when (ex.Status is 403 or 404) { }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { DeferDelivered(thread, ids); await RecordErrorAsync(ex); break; }
        }
        await SaveDeliveredAsync();
    }

    private sealed record DeliveredReceipt(Guid Thread, List<Guid> Pending, List<Guid>? Deferred, Guid Mark, DateTime MarkAt);
}
