using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.Integration.Security;

public sealed class IdentityServerHttpActorIdentityProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceIdentityOptions> options,
    IServiceTokenProvider serviceTokenProvider,
    ILogger<IdentityServerHttpActorIdentityProvider> logger)
    : IActorIdentityProvider
{
    internal const string ClientName = "XFramework.IdentityServer.ActorValidation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ActorIdentityValidationResult> ValidateAsync(
        string token,
        CancellationToken ct = default)
    {
        try
        {
            var endpoint = new Uri(options.Value.ResolveAuthority(), "/api/auth/validate-session");
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new ValidateIdentitySessionRequest
                {
                    Metadata = new RequestMetadata
                    {
                        RequestId = Guid.NewGuid(),
                        OperationName = "Validate actor identity",
                        DeviceName = Environment.MachineName
                    }
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var serviceToken = await serviceTokenProvider.GetTokenAsync(
                XFrameworkServiceNames.IdentityServer,
                [XFrameworkServiceScopes.IdentitySessionValidate],
                ct);
            request.Headers.TryAddWithoutValidation(
                "X-XFramework-Service-Authorization",
                $"Bearer {serviceToken}");

            var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? (int)response.StatusCode
                    : 503;
                return ActorIdentityValidationResult.Failure(
                    statusCode == 503
                        ? "Actor identity validation is unavailable."
                        : "Actor identity is invalid.",
                    statusCode);
            }

            var snapshot = await response.Content.ReadFromJsonAsync<ValidateIdentitySessionResponse>(JsonOptions, ct);
            if (snapshot is not { IsValid: true } ||
                snapshot.TenantId == Guid.Empty ||
                snapshot.CredentialId == Guid.Empty ||
                snapshot.SessionId == Guid.Empty ||
                snapshot.IdentityId == Guid.Empty ||
                string.IsNullOrWhiteSpace(snapshot.GenerationId))
            {
                return ActorIdentityValidationResult.Failure(
                    "IdentityServer returned an incomplete actor identity.",
                    503);
            }

            return ActorIdentityValidationResult.Success(new TrustedActorIdentity(
                snapshot.CredentialId,
                snapshot.IdentityId,
                snapshot.TenantId,
                snapshot.SessionId,
                snapshot.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase),
                snapshot.Capabilities.ToHashSet(StringComparer.OrdinalIgnoreCase),
                snapshot.GenerationId,
                new DateTimeOffset(DateTime.SpecifyKind(snapshot.ExpiresAtUtc, DateTimeKind.Utc)),
                snapshot.Attributes));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "IdentityServer HTTP actor validation failed closed.");
            return ActorIdentityValidationResult.Failure(
                "Actor identity validation is unavailable.",
                503);
        }
    }
}
