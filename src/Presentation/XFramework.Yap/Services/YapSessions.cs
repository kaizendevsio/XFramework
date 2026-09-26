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
public sealed class YapSessions(IDistributedCache cache, IDataProtectionProvider protection,
    IServiceScopeFactory scopes, TimeProvider clock)
{
    // An installed phone messenger must not sign you out while you keep using it, so the
    // sign-in only ever ends on disuse, never on age: each use pushes the idle deadline out
    // again, and there is no absolute cap. Sign-out, upstream revocation and credential
    // changes still end it immediately. The upstream session is persistent too (see
    // YapAuth), so the one other bound is IdentityServer's refresh token: it lives 14 days
    // and slides on each rotation. This window is half of that, leaving room for clock skew,
    // a backgrounded app and a transient refresh failure, so a returning device always
    // arrives with something left to refresh with.
    public static readonly TimeSpan IdleWindow = TimeSpan.FromDays(7);

    // Rolling on every request would write the shared store on every request. The deadline
    // only moves once it has drifted this far, which costs one write per active hour.
    private static readonly TimeSpan RollSlack = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Refreshing rotates the refresh token, so two concurrent requests for one session
    // must not both refresh. This gate covers a single instance, which is the deployed shape.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new();

    private readonly IDataProtector protector = protection.CreateProtector("Yap.Sessions.v1");

    public async ValueTask<bool> ContainsAsync(ClaimsPrincipal? user, CancellationToken ct = default) =>
        user?.Identity?.IsAuthenticated == true &&
        user.FindFirstValue(YapAuth.SessionClaim) is { } key &&
        await ReadAsync(key, ct) is not null;

    /// <summary>Records use and returns the deadline the sign-in now holds, or null once the
    /// idle window lapsed or sign-out revoked it.</summary>
    public async ValueTask<DateTimeOffset?> TouchAsync(ClaimsPrincipal? user, CancellationToken ct = default)
    {
        if (user?.Identity?.IsAuthenticated != true || user.FindFirstValue(YapAuth.SessionClaim) is not { } key ||
            await ReadAsync(key, ct) is not { } entry) return null;
        if (Roll(entry)) await WriteAsync(key, entry, ct);
        return entry.ActiveUntil;
    }

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
            ExpiresAt = clock.GetUtcNow().AddSeconds(response.ExpiresIn),
            ActiveUntil = clock.GetUtcNow().Add(IdleWindow)
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
            // Chat work is use like any other. A socket that stays open for days never
            // revalidates the cookie, so the actor path has to roll the sign-in itself.
            var rolled = Roll(entry);
            if (entry.ExpiresAt <= clock.GetUtcNow().AddSeconds(60))
            {
                using var scope = scopes.CreateScope();
                // Once rotation starts it must finish and persist even if the browser crashes
                // and aborts its request. Otherwise the next request reuses the spent token.
                using var refreshTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var identity = scope.ServiceProvider.GetRequiredService<IIdentityServerServiceWrapper>();
                var response = await identity.RefreshToken(new RefreshTokenRequest
                {
                    AccessToken = entry.AccessToken, RefreshToken = entry.RefreshToken, SessionId = entry.SessionId,
                    Metadata = new RequestMetadata { RequestedTenantId = entry.TenantId, RequestId = Guid.NewGuid() }
                }, refreshTimeout.Token);
                if (!response.IsSuccess)
                {
                    if (response.HttpStatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    {
                        await cache.RemoveAsync(CacheKey(key), CancellationToken.None);
                        throw new UnauthorizedAccessException("Your session ended. Please sign in again.");
                    }
                    // A timeout, rate limit or upstream outage must not destroy the refresh session.
                    throw new HttpRequestException("Session refresh is temporarily unavailable.");
                }
                if (response.Response is not { } tokens ||
                    tokens.SessionId != entry.SessionId || string.IsNullOrWhiteSpace(tokens.AccessToken) ||
                    string.IsNullOrWhiteSpace(tokens.RefreshToken) || tokens.ExpiresIn <= 0)
                {
                    throw new HttpRequestException("Session refresh returned an incomplete response.");
                }
                entry.AccessToken = tokens.AccessToken;
                entry.RefreshToken = tokens.RefreshToken;
                entry.ExpiresAt = clock.GetUtcNow().AddSeconds(tokens.ExpiresIn);
                await WriteAsync(key, entry, CancellationToken.None);
            }
            else if (rolled) await WriteAsync(key, entry, ct);
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

    // Use pushes the idle deadline out. Returns whether the move is worth a write.
    private bool Roll(Entry entry)
    {
        var rolled = clock.GetUtcNow().Add(IdleWindow);
        if (rolled - entry.ActiveUntil < RollSlack) return false;
        entry.ActiveUntil = rolled;
        return true;
    }

    private async ValueTask<Entry?> ReadAsync(string key, CancellationToken ct)
    {
        var stored = await cache.GetAsync(CacheKey(key), ct);
        if (stored is null || stored.Length == 0) return null;
        try
        {
            var entry = JsonSerializer.Deserialize<Entry>(protector.Unprotect(Encoding.UTF8.GetString(stored)), Json);
            if (entry is null) return null;
            // Entries written before sign-ins rolled carry no idle deadline. Honour the one
            // deadline they do have instead of signing everybody out on the rollout.
            if (entry.ActiveUntil == default) entry.ActiveUntil = entry.SignedOutAt;
            return entry.ActiveUntil <= clock.GetUtcNow() ? null : entry;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Written under a Data Protection key this instance can no longer read.
            return null;
        }
    }

    private async Task WriteAsync(string key, Entry entry, CancellationToken ct)
    {
        // The cache TTL tracks the rolling deadline, not the cap: a shorter TTL would let
        // Redis evict a session the app still considers valid, a longer one would keep
        // ciphertext around past the sign-in it belongs to.
        var remaining = entry.ActiveUntil - clock.GetUtcNow();
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
        /// <summary>The rolling idle deadline: each use pushes it out again.</summary>
        public DateTimeOffset ActiveUntil { get; set; }
        /// <summary>Read only from entries written before sign-ins rolled, which carry no
        /// <see cref="ActiveUntil"/>. Sign-ins no longer have an absolute cap.</summary>
        public DateTimeOffset SignedOutAt { get; set; }
    }
}
