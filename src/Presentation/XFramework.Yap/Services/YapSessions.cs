using System.Security.Claims;
using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.Extensions.Caching.Memory;
using XFramework.Domain.Shared.BusinessObjects;

namespace Yap.Services;

// Tokens stay in the server process. The encrypted cookie contains only identity claims
// and an opaque session key. Restarting this single-instance app requires signing in again.
public sealed class YapSessions(IMemoryCache cache, IServiceScopeFactory scopes)
{
    public bool Contains(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        user.FindFirstValue(YapAuth.SessionClaim) is { } key && cache.TryGetValue<Entry>(key, out _);

    public ClaimsPrincipal Create(AuthenticateIdentityResponse response)
    {
        if (response.Credential is not { Id: var credential, TenantId: var tenant } ||
            credential == Guid.Empty || tenant == Guid.Empty || response.SessionId is not { } session ||
            session == Guid.Empty || string.IsNullOrWhiteSpace(response.AccessToken) ||
            string.IsNullOrWhiteSpace(response.RefreshToken) || response.ExpiresIn <= 0)
            throw new InvalidOperationException("IdentityServer returned an incomplete session.");

        var key = Guid.NewGuid().ToString("N");
        var name = response.Identity?.FullName;
        if (string.IsNullOrWhiteSpace(name)) name = response.Credential.UserAlias ?? response.Credential.UserName ?? "You";
        cache.Set(key, new Entry(tenant, credential, session, response.AccessToken,
            response.RefreshToken, DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn)), TimeSpan.FromHours(8));
        return new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, credential.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(YapAuth.TenantClaim, tenant.ToString()),
            new Claim(YapAuth.SessionClaim, key)
        ], YapAuth.Scheme));
    }

    public async ValueTask<CommunicationsChatActor> GetActorAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var key = user.FindFirstValue(YapAuth.SessionClaim);
        if (user.Identity?.IsAuthenticated != true || key is null ||
            !cache.TryGetValue<Entry>(key, out var entry) || entry is null)
            throw new UnauthorizedAccessException("Your session ended. Please sign in again.");

        await entry.Gate.WaitAsync(ct);
        try
        {
            if (!cache.TryGetValue<Entry>(key, out var current) || current != entry)
                throw new UnauthorizedAccessException("Your session ended. Please sign in again.");
            if (entry.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(60))
            {
                using var scope = scopes.CreateScope();
                var identity = scope.ServiceProvider.GetRequiredService<IIdentityServerServiceWrapper>();
                var response = await identity.RefreshToken(new RefreshTokenRequest
                {
                    AccessToken = entry.AccessToken, RefreshToken = entry.RefreshToken, SessionId = entry.SessionId,
                    Metadata = new RequestMetadata { RequestedTenantId = entry.TenantId, RequestId = Guid.NewGuid() }
                }, ct);
                if (!response.IsSuccess || response.Response is not { } tokens ||
                    tokens.SessionId != entry.SessionId || string.IsNullOrWhiteSpace(tokens.AccessToken) ||
                    string.IsNullOrWhiteSpace(tokens.RefreshToken) || tokens.ExpiresIn <= 0)
                {
                    cache.Remove(key);
                    throw new UnauthorizedAccessException("Your session ended. Please sign in again.");
                }
                entry.AccessToken = tokens.AccessToken;
                entry.RefreshToken = tokens.RefreshToken;
                entry.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);
            }
            return new CommunicationsChatActor(entry.TenantId, entry.CredentialId, key, entry.AccessToken);
        }
        finally { entry.Gate.Release(); }
    }

    public async Task RevokeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var key = user.FindFirstValue(YapAuth.SessionClaim);
        if (key is null || !cache.TryGetValue<Entry>(key, out var entry) || entry is null) return;
        cache.Remove(key);
        await entry.Gate.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityServerServiceWrapper>();
            var tokenScope = scope.ServiceProvider.GetRequiredService<XFramework.Integration.Security.IActorAccessTokenScope>();
            using var actor = tokenScope.Push(entry.AccessToken);
            var result = await identity.Logout(new LogoutRequest
            {
                SessionId = entry.SessionId, CredentialId = entry.CredentialId,
                Metadata = new RequestMetadata { RequestedTenantId = entry.TenantId, RequestId = Guid.NewGuid() }
            }, ct);
            ChatWorkspace.Require(result);
        }
        finally { entry.Gate.Release(); }
    }

    private sealed class Entry(Guid tenantId, Guid credentialId, Guid sessionId, string accessToken,
        string refreshToken, DateTimeOffset expiresAt)
    {
        public Guid TenantId { get; } = tenantId;
        public Guid CredentialId { get; } = credentialId;
        public Guid SessionId { get; } = sessionId;
        public string AccessToken { get; set; } = accessToken;
        public string RefreshToken { get; set; } = refreshToken;
        public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
        public SemaphoreSlim Gate { get; } = new(1, 1);
    }
}
