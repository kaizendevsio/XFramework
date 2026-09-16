using System.Collections.Concurrent;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Integration.Drivers;

namespace Yap.Services;

public sealed partial class YapCallGateway
{
    private readonly ConcurrentDictionary<Guid, RecordCallRequest> pendingHistory = new();
    private readonly SemaphoreSlim historyWriter = new(1, 1);

    // The call ID is also the message ID. Retries and competing socket/leave callbacks
    // therefore create exactly one history entry in Communications.
    private void QueueCallHistory(Guid tenant, Guid thread, Guid call, Guid caller, DateTimeOffset? connected)
    {
        pendingHistory.TryAdd(call, new RecordCallRequest
        {
            CallId = call, ThreadId = thread, CallerId = caller,
            ConnectedAt = connected, EndedAt = DateTimeOffset.UtcNow, Metadata = YapPush.Metadata(tenant)
        });
        _ = Task.Run(FlushCallHistoryAsync);
    }

    internal async Task FlushCallHistoryAsync()
    {
        if (!await historyWriter.WaitAsync(0)) return;
        try
        {
            foreach (var (id, request) in pendingHistory.ToArray())
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var communications = scope.ServiceProvider.GetRequiredService<ICommunicationsServiceWrapper>();
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var result = await communications.RecordCall(request, deadline.Token);
                    if (result.IsSuccess) pendingHistory.TryRemove(id, out _);
                    else pushLogger.LogWarning("Call history write failed with status {Status}; it will be retried", result.HttpStatusCode);
                }
                catch (Exception ex) { pushLogger.LogWarning(ex, "Call history write failed; it will be retried"); }
            }
        }
        finally { historyWriter.Release(); }
    }

    private void RemoveGroupLocked(GroupRoom room)
    {
        if (groups.Remove(room.Id))
            QueueCallHistory(room.Tenant, room.Thread, room.Id, room.Caller, room.ConnectedAt);
    }
}
