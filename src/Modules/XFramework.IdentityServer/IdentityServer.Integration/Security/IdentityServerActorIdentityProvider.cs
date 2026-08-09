using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
            var response = await identityServer.ValidateIdentitySession(
                new ValidateIdentitySessionRequest
                {
                    Metadata = new RequestMetadata
                    {
                        RequestId = Guid.NewGuid(),
                        OperationName = "Validate actor identity",
                        DeviceName = Environment.MachineName
                    }
                },
                ct);

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

    private sealed record CachedValidation(string TokenDigest, ActorIdentityValidationResult Result);
}
