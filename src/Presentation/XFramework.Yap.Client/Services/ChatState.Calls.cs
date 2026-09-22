using System.Text.Json;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    private long callHistoryVersion;
    public async Task<ChatPage<CallHistoryItem>> CallsAsync(int page = 0)
    {
        if (User is null) return new([], 0);
        var scope = Scope;
        var version = callHistoryVersion;
        var key = $"calls:{scope}:{page}";
        if (Online && SessionReady)
        {
            try
            {
                var result = await api.GetAsync<ChatPage<CallHistoryItem>>($"api/chat/calls/history?page={page}");
                // A response started before logout/deletion must not repopulate discarded history.
                await sync.WaitAsync(lifetime.Token);
                try
                {
                    if (scope != Scope || version != callHistoryVersion) return new([], 0);
                    await store.SetSettingAsync(key, JsonSerializer.Serialize(result));
                    return result;
                }
                finally { sync.Release(); }
            }
            catch (Exception ex) when (IsConnectionFailure(ex)) { SetOffline(); Notify(); }
        }
        var cached = await store.SettingAsync(key);
        return scope == Scope && version == callHistoryVersion && cached is not null ? JsonSerializer.Deserialize<ChatPage<CallHistoryItem>>(cached) ?? new([], 0) : new([], 0);
    }
}
