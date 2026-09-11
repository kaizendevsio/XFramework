using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace IdentityServer.Integration.Security;

public sealed class IdentityServerActorIdentityProvider(
    IIdentityServerServiceWrapper identityServer,
    IActorAccessTokenScope actorAccessTokenScope,
    IHttpContextAccessor httpContextAccessor,
    ILogger<IdentityServerActorIdentityProvider> logger)
    : IActorIdentityProvider
{
    private static readonly object RequestCacheKey = new();
    private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromMilliseconds(250);

    public async Task<ActorIdentityValidationResult> ValidateAsync(
        string token,
        CancellationToken ct = default)
    {
        var tokenDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(RequestCacheKey, out var cachedValue) == true &&
            cachedValue is CachedValidation cached &&
            string.Equals(cached.TokenDigest, tokenDigest, StringComparison.Ordinal))
        {
            return cached.Result;
        }

        try
        {
            using var actorScope = actorAccessTokenScope.Push(token);
            var response = await ValidateWithTransientRetryAsync(ct);

            if (!response.IsSuccess || response.Response is not { IsValid: true } snapshot)
            {
                return ActorIdentityValidationResult.Failure(
                    response.Message ?? "Actor identity is invalid.",
                    (int)response.HttpStatusCode);
            }

            if (snapshot.TenantId == Guid.Empty ||
                snapshot.CredentialId == Guid.Empty ||
                snapshot.SessionId == Guid.Empty ||
                snapshot.IdentityId == Guid.Empty ||
                string.IsNullOrWhiteSpace(snapshot.GenerationId))
            {
                return ActorIdentityValidationResult.Failure(
                    "IdentityServer returned an incomplete actor identity.",
                    503);
            }

            var result = ActorIdentityValidationResult.Success(new TrustedActorIdentity(
                snapshot.CredentialId,
                snapshot.IdentityId,
                snapshot.TenantId,
                snapshot.SessionId,
                snapshot.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase),
                snapshot.Capabilities.ToHashSet(StringComparer.OrdinalIgnoreCase),
                snapshot.GenerationId,
                new DateTimeOffset(DateTime.SpecifyKind(snapshot.ExpiresAtUtc, DateTimeKind.Utc)),
                snapshot.Attributes));
            if (httpContext is not null)
                httpContext.Items[RequestCacheKey] = new CachedValidation(tokenDigest, result);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "IdentityServer Bolt actor validation failed closed.");
            return ActorIdentityValidationResult.Failure(
                "Actor identity validation is unavailable.",
                503);
        }
    }

    private async Task<QueryResponse<ValidateIdentitySessionResponse>> ValidateWithTransientRetryAsync(
        CancellationToken ct)
    {
        try
        {
            var response = await identityServer.ValidateIdentitySession(CreateRequest(), ct);
            if (response.HttpStatusCode != HttpStatusCode.ServiceUnavailable)
                return response;

            logger.LogDebug("Retrying actor validation after a transient IdentityServer service-unavailable response.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            logger.LogDebug(exception, "Retrying actor validation after a transient Bolt transport failure.");
        }

        await Task.Delay(TransientRetryDelay, ct);
        return await identityServer.ValidateIdentitySession(CreateRequest(), ct);
    }

    private static ValidateIdentitySessionRequest CreateRequest() =>
        new()
        {
            Metadata = new RequestMetadata
            {
                RequestId = Guid.NewGuid(),
                OperationName = "Validate actor identity",
                DeviceName = Environment.MachineName
            }
        };

    private sealed record CachedValidation(string TokenDigest, ActorIdentityValidationResult Result);
}
