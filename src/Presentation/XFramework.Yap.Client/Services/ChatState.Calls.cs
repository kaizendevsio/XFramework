using System.Text.Json;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    private long callHistoryVersion;
    private (string Key, DateTime At, ChatPage<CallHistoryItem> Page)? callsPage;
    // A new call summary in any conversation changes this, so a finished call is never hidden by reuse.
    private string CallsKey(string scope) => $"{scope}|{callHistoryVersion}|" + string.Join(',', Conversations
        .Where(c => c.LastMessage?.IsCallSummary == true).Select(c => c.LastMessage!.Id).Order());
    public async Task<ChatPage<CallHistoryItem>> CallsAsync(int page = 0)
    {
        if (User is null) return new([], 0);
        var scope = Scope;
        var version = callHistoryVersion;
        var key = $"calls:{scope}:{page}";
        if (Online && SessionReady)
        {
            var reuse = CallsKey(scope);
            if (page == 0 && callsPage is { } recent && recent.Key == reuse && DateTime.UtcNow - recent.At < PageReuse)
                return recent.Page with { Items = [.. recent.Page.Items] };
            try
            {
                var result = await api.GetAsync<ChatPage<CallHistoryItem>>($"api/chat/calls/history?page={page}");
                // A response started before logout/deletion must not repopulate discarded history.
                await sync.WaitAsync(lifetime.Token);
                try
                {
                    if (scope != Scope || version != callHistoryVersion) return new([], 0);
                    await store.SetSettingAsync(key, JsonSerializer.Serialize(result));
                    if (page == 0) callsPage = (reuse, DateTime.UtcNow, result with { Items = [.. result.Items] });
                    return result;
                }
                finally { sync.Release(); }
            }
            catch (ChatApiException ex) when (ex.SessionEnded) { EndedSession(ex); Notify(); }
            catch (Exception ex) when (IsConnectionFailure(ex)) { SetOffline(); Notify(); }
        }
        var cached = await store.SettingAsync(key);
        return scope == Scope && version == callHistoryVersion && cached is not null ? JsonSerializer.Deserialize<ChatPage<CallHistoryItem>>(cached) ?? new([], 0) : new([], 0);
    }
}
