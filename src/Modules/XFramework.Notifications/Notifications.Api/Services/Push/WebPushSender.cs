using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Headers;

namespace Notifications.Api.Services.Push;

public enum WebPushOutcome
{
    /// <summary>The push service accepted the message.</summary>
    Delivered,

    /// <summary>404/410: the subscription is gone for good and its row must be deleted, not retried.</summary>
    Gone,

    /// <summary>413: the encrypted record exceeded the push service limit. Retrying the same payload cannot help.</summary>
    TooLarge,

    /// <summary>Network trouble or 5xx: worth another attempt later.</summary>
    Transient,

    /// <summary>4xx other than the above, usually a rejected VAPID token or malformed keys.</summary>
    Rejected
}

public sealed record WebPushResult(WebPushOutcome Outcome, int StatusCode, string? Detail = null);

/// <summary>Posts one RFC 8291 encrypted record to a push service endpoint with a VAPID Authorization header.</summary>
public sealed class WebPushSender(IHttpClientFactory httpClientFactory, ILogger<WebPushSender> logger)
{
    public const string HttpClientName = "web-push";

    public async Task<WebPushResult> SendAsync(
        WebPushVapidKeys keys,
        string endpoint,
        string p256dh,
        string auth,
        ReadOnlyMemory<byte> payload,
        int ttlSeconds,
        string urgency,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return new WebPushResult(WebPushOutcome.Rejected, 0, "The push endpoint is not an absolute https URL.");

        if (!PushBase64Url.TryDecode(p256dh, 65, out var receiverKey) ||
            !PushBase64Url.TryDecode(auth, 16, out var authSecret))
        {
            return new WebPushResult(WebPushOutcome.Gone, 0, "The stored subscription keys are unusable.");
        }

        byte[] body;
        try
        {
            body = WebPushCrypto.Encrypt(payload.Span, receiverKey, authSecret);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            return new WebPushResult(WebPushOutcome.Gone, 0, ex.Message);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.TryAddWithoutValidation("Authorization", VapidSigner.CreateAuthorizationHeader(keys, uri, DateTimeOffset.UtcNow));
        request.Headers.TryAddWithoutValidation("TTL", Math.Clamp(ttlSeconds, 0, 2419200).ToString());
        request.Headers.TryAddWithoutValidation("Urgency", urgency);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");

        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return new WebPushResult(WebPushOutcome.Delivered, status);

            return response.StatusCode switch
            {
                HttpStatusCode.NotFound or HttpStatusCode.Gone =>
                    new WebPushResult(WebPushOutcome.Gone, status, "The push service reports this subscription is gone."),
                HttpStatusCode.RequestEntityTooLarge =>
                    new WebPushResult(WebPushOutcome.TooLarge, status, "The push service rejected the payload size."),
                HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError =>
                    new WebPushResult(WebPushOutcome.Transient, status, $"The push service returned {status}."),
                _ => new WebPushResult(WebPushOutcome.Rejected, status, await ReadDetailAsync(response, ct))
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug(ex, "Web push request to {PushHost} failed", uri.Host);
            return new WebPushResult(WebPushOutcome.Transient, 0, ex.Message);
        }
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            return text.Length <= 256 ? text : text[..256];
        }
        catch
        {
            return $"The push service returned {(int)response.StatusCode}.";
        }
    }
}
