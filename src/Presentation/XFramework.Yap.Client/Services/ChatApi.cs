using System.Net.Http.Json;
using System.Text.Json;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed class ChatApi(HttpClient http)
{
    public string Token { get; set; } = "";
    public string Account { get; set; } = "";
    public Func<string, object?, CancellationToken, Task<ChatSocketResponse?>>? SocketRequest { get; set; }
    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var route = path.Split('?')[0];
        if (route.StartsWith("api/chat/conversations/", StringComparison.Ordinal) && route.EndsWith("/messages", StringComparison.Ordinal))
            path += (path.Contains('?') ? "&" : "?") + "acknowledge=false";
        var account = Account;
        try { return (await SendAsync<T>(HttpMethod.Get, path, null, ct))!; }
        catch (ChatApiException ex) when (ex.Status == 503 && !ct.IsCancellationRequested)
        {
            // A reconnecting upstream can briefly reject a read. Retry once before
            // falling back to the offline poll; never replay a write here.
            await Task.Delay(150, ct);
            if (Account != account) throw new OperationCanceledException("The signed-in account changed.");
            return (await SendAsync<T>(HttpMethod.Get, path, null, ct))!;
        }
    }
    public async Task<T?> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
    {
        var account = Account;
        var operation = path switch
        {
            "api/chat/messages" => "send",
            "api/chat/read" => "read",
            "api/chat/delivered" => "delivered",
            "api/chat/thread-actions" when body is ThreadAction { Action: "typing" } => "typing",
            _ => null
        };
        if (operation is not null && SocketRequest is not null)
        {
            // Null means no request was sent. An uncertain socket failure throws:
            // the durable outbox retries the same message ID/ciphertext later.
            var response = await SocketRequest(operation, body, ct);
            if (account != Account) throw new OperationCanceledException("The signed-in account changed.");
            if (response is not null)
            {
                if (response.Status is < 200 or >= 300) throw new ChatApiException(response.Status) { SessionEnded = response.SessionEnded };
                return response.Body is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } data
                    ? data.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : default;
            }
        }
        return await SendAsync<T>(HttpMethod.Post, path, body is null ? null : JsonContent.Create(body), ct);
    }
    public Task PostAsync(string path, object body, CancellationToken ct = default) => PostAsync<object>(path, body, ct);
    public async Task<string> AuthenticateAsync(string action, Dictionary<string, string> fields)
    {
        var session = await GetAsync<SessionResponse>("api/session");
        Token = session.AntiforgeryToken;
        var result = await SendAsync<AuthRedirect>(HttpMethod.Post, $"api/auth/{action}", new FormUrlEncodedContent(fields));
        return result!.Redirect;
    }
    private async Task<T?> SendAsync<T>(HttpMethod method, string path, HttpContent? body, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body };
        request.Headers.TryAddWithoutValidation("X-Yap-Account", Account);
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", Token);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ChatApiException((int)response.StatusCode)
        {
            SessionEnded = response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                response.Headers.TryGetValues(SessionSignal.Header, out var signal) && signal.Contains(SessionSignal.Ended)
        };
        return response.StatusCode == System.Net.HttpStatusCode.NoContent ? default : await response.Content.ReadFromJsonAsync<T>(ct);
    }
    private sealed record AuthRedirect(string Redirect);
}

public sealed class ChatApiException(int status) : Exception(status switch
{
    401 => "Your session ended. Sign in again to sync; saved conversations are still available.",
    403 => "You no longer have permission to do this.",
    409 => "This change conflicts with a message already saved. Review it before retrying.",
    410 => "Re-attach this file. A large upload cannot resume after the app reloads.",
    413 => "This attachment is larger than Yap accepts.",
    428 => "Waiting for everyone in this conversation to open Yap and set up encrypted messages.",
    429 => "Too many requests. Your messages will retry shortly.",
    >= 500 => "Chat is temporarily unavailable. Your messages remain on this device.",
    _ => "The request could not be completed. Check the message and try again."
})
{
    public int Status { get; } = status;
    /// <summary>The server confirmed the sign-in itself ended, which only a refused refresh
    /// does. Without this a 401 proves nothing about the sign-in.</summary>
    public bool SessionEnded { get; init; }
}
