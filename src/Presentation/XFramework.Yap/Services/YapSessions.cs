using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using XFramework.Domain.Shared.BusinessObjects;

namespace Yap.Services;

// Tokens never reach the browser: the encrypted cookie carries identity claims and an
// opaque session key, and the tokens themselves live in the session store under that key.
// The store is distributed so a restart or a rollout no longer signs everyone out, and the
// payload is sealed with Data Protection so the shared cache only ever holds ciphertext.
public sealed class YapSessions(IDistributedCache cache, IDataProtectionProvider protection, IServiceScopeFactory scopes)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Refreshing rotates the refresh token, so two concurrent requests for one session
    // must not both refresh. This gate covers a single instance, which is the deployed shape.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new();

    private readonly IDataProtector protector = protection.CreateProtector("Yap.Sessions.v1");

    public async ValueTask<bool> ContainsAsync(ClaimsPrincipal? user, CancellationToken ct = default) =>
        user?.Identity?.IsAuthenticated == true &&
        user.FindFirstValue(YapAuth.SessionClaim) is { } key &&
        await ReadAsync(key, ct) is not null;

    public async Task<ClaimsPrincipal> CreateAsync(AuthenticateIdentityResponse response, CancellationToken ct = default)
    {
        if (response.Credential is not { Id: var credential, TenantId: var tenant } ||
            credential == Guid.Empty || tenant == Guid.Empty || response.SessionId is not { } session ||
            session == Guid.Empty || string.IsNullOrWhiteSpace(response.AccessToken) ||
            string.IsNullOrWhiteSpace(response.RefreshToken) || response.ExpiresIn <= 0)
            throw new InvalidOperationException("IdentityServer returned an incomplete session.");

        var key = Guid.NewGuid().ToString("N");
        var name = response.Identity?.FullName;
        if (string.IsNullOrWhiteSpace(name)) name = response.Credential.UserAlias ?? response.Credential.UserName ?? "You";
        await WriteAsync(key, new Entry
        {
            TenantId = tenant, CredentialId = credential, SessionId = session,
            AccessToken = response.AccessToken, RefreshToken = response.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn),
            SignedOutAt = DateTimeOffset.UtcNow.Add(Lifetime)
        }, ct);
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
        if (user.Identity?.IsAuthenticated != true || key is null)
            throw new UnauthorizedAccessException("Your session ended. Please sign in again.");

        var gate = gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var entry = await ReadAsync(key, ct) ?? throw new UnauthorizedAccessException("Your session ended. Please sign in again.");
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
                    await cache.RemoveAsync(CacheKey(key), ct);
                    throw new UnauthorizedAccessException("Your session ended. Please sign in again.");
                }
                entry.AccessToken = tokens.AccessToken;
                entry.RefreshToken = tokens.RefreshToken;
                entry.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);
                await WriteAsync(key, entry, ct);
            }
            return new CommunicationsChatActor(entry.TenantId, entry.CredentialId, key, entry.AccessToken);
        }
        finally { gate.Release(); }
    }

    public async Task RevokeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var key = user.FindFirstValue(YapAuth.SessionClaim);
        if (key is null) return;
        var entry = await ReadAsync(key, ct);
        await cache.RemoveAsync(CacheKey(key), ct);
        gates.TryRemove(key, out _);
        if (entry is null) return;
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

    private static string CacheKey(string key) => $"yap:session:{key}";

    private async ValueTask<Entry?> ReadAsync(string key, CancellationToken ct)
    {
        var stored = await cache.GetAsync(CacheKey(key), ct);
        if (stored is null || stored.Length == 0) return null;
        try
        {
            var entry = JsonSerializer.Deserialize<Entry>(protector.Unprotect(Encoding.UTF8.GetString(stored)), Json);
            // A rewritten entry must never outlive the sign-in it belongs to.
            return entry is null || entry.SignedOutAt <= DateTimeOffset.UtcNow ? null : entry;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Written under a Data Protection key this instance can no longer read.
            return null;
        }
    }

    private async Task WriteAsync(string key, Entry entry, CancellationToken ct)
    {
        var remaining = entry.SignedOutAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return;
        var payload = Encoding.UTF8.GetBytes(protector.Protect(JsonSerializer.Serialize(entry, Json)));
        await cache.SetAsync(CacheKey(key), payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining }, ct);
    }

    private sealed class Entry
    {
        public Guid TenantId { get; set; }
        public Guid CredentialId { get; set; }
        public Guid SessionId { get; set; }
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        /// <summary>When the access token needs refreshing.</summary>
        public DateTimeOffset ExpiresAt { get; set; }
        /// <summary>When the sign-in itself lapses; refreshes never extend this.</summary>
        public DateTimeOffset SignedOutAt { get; set; }
    }
}
