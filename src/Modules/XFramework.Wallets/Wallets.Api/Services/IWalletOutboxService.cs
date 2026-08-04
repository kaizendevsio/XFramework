using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Wallets.Api.Events;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public interface IWalletOutboxService
{
    Task<Result<WalletOutboxActionResponse>> RetryAsync(WalletOutboxActionRequest request, CancellationToken ct = default);
    Task<Result<WalletOutboxActionResponse>> ReplayAsync(WalletOutboxActionRequest request, CancellationToken ct = default);
    Task<Result<WalletOutboxActionResponse>> DeadLetterAsync(WalletOutboxActionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetDueTenantIdsAsync(CancellationToken ct = default);
    Task DispatchDueAsync(CancellationToken ct = default);
}

public interface IWalletOutboxPublisher
{
    Task PublishAsync(WalletOutboxMessage message, CancellationToken ct = default);
}

public sealed class WalletOutboxPublisher(
    IWalletEventPublisher eventPublisher,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WalletOutboxPublisher> logger) : IWalletOutboxPublisher
{
    public async Task PublishAsync(WalletOutboxMessage message, CancellationToken ct = default)
    {
        var deliveryUrls = GetDeliveryUrls();
        if (deliveryUrls.Count > 0)
        {
            await PublishHttpAsync(message, deliveryUrls, ct);
            return;
        }

        var (walletId, credentialId) = ReadEventIdentity(message);
        await eventPublisher.PublishAsync(new WalletEvent
        {
            EventType = message.EventType,
            TenantId = message.TenantId,
            WalletId = walletId,
            CredentialId = credentialId
        });

        logger.LogInformation(
            "Published wallet outbox message {OutboxMessageId} of type {EventType}",
            message.Id,
            message.EventType);
    }

    private async Task PublishHttpAsync(WalletOutboxMessage message, IReadOnlyList<string> deliveryUrls, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(nameof(WalletOutboxPublisher));
        var signature = CreateSignature(message.PayloadJson);

        foreach (var deliveryUrl in deliveryUrls)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, deliveryUrl)
            {
                Content = new StringContent(message.PayloadJson, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.TryAddWithoutValidation("X-Wallet-Event-Type", message.EventType);
            httpRequest.Headers.TryAddWithoutValidation("X-Wallet-Outbox-Message-Id", message.Id.ToString());
            httpRequest.Headers.TryAddWithoutValidation("X-Wallet-Tenant-Id", message.TenantId.ToString());
            if (!string.IsNullOrWhiteSpace(signature))
            {
                httpRequest.Headers.TryAddWithoutValidation("X-Wallet-Signature", signature);
            }

            using var response = await client.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Wallet outbox delivery to {deliveryUrl} failed with {(int)response.StatusCode}: {responseBody}");
            }
        }

        logger.LogInformation(
            "Delivered wallet outbox message {OutboxMessageId} to {DeliveryCount} configured endpoint(s)",
            message.Id,
            deliveryUrls.Count);
    }

    private IReadOnlyList<string> GetDeliveryUrls()
    {
        var urls = configuration.GetSection("Wallets:Outbox:WebhookUrls").Get<string[]>()
            ?? configuration.GetSection("Wallets:Outbox:DeliveryUrls").Get<string[]>()
            ?? [];
        var single = configuration["Wallets:Outbox:WebhookUrl"]
            ?? configuration["Wallets:Outbox:DeliveryUrl"];

        return urls
            .Concat(string.IsNullOrWhiteSpace(single) ? [] : [single])
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? CreateSignature(string payload)
    {
        var secret = configuration["Wallets:Outbox:WebhookSecret"]
            ?? configuration["Wallets:Webhooks:SharedSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static (Guid WalletId, Guid CredentialId) ReadEventIdentity(WalletOutboxMessage message)
    {
        try
        {
            using var document = JsonDocument.Parse(message.PayloadJson);
            var root = document.RootElement;
            var walletId = TryGetGuid(root, "walletId");
            var credentialId = TryGetGuid(root, "actorCredentialId");
            return (walletId, credentialId);
        }
        catch (JsonException)
        {
            return (Guid.Empty, Guid.Empty);
        }
    }

    private static Guid TryGetGuid(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Guid.Empty;
        }

        return Guid.TryParse(property.GetString(), out var id) ? id : Guid.Empty;
    }
}
