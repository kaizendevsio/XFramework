using System.Net.Http.Json;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed class ChatApi(HttpClient http)
{
    public string Token { get; set; } = "";
    public string Account { get; set; } = "";
    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default) =>
        (await SendAsync<T>(HttpMethod.Get, path, null, ct))!;
    public Task<T?> PostAsync<T>(string path, object? body = null, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Post, path, body is null ? null : JsonContent.Create(body), ct);
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
        if (!response.IsSuccessStatusCode) throw new ChatApiException((int)response.StatusCode);
        return response.StatusCode == System.Net.HttpStatusCode.NoContent ? default : await response.Content.ReadFromJsonAsync<T>(ct);
    }
    private sealed record AuthRedirect(string Redirect);
}

public sealed class ChatApiException(int status) : Exception(status switch
{
    401 => "Your session ended. Sign in again to sync; saved conversations are still available.",
    403 => "You no longer have permission to do this.",
    409 => "This change conflicts with a message already saved. Review it before retrying.",
    413 => "This attachment is too large. The limit is 20 MB.",
    429 => "Too many requests. Your messages will retry shortly.",
    >= 500 => "Chat is temporarily unavailable. Your messages remain on this device.",
    _ => "The request could not be completed. Check the message and try again."
}) { public int Status { get; } = status; }
