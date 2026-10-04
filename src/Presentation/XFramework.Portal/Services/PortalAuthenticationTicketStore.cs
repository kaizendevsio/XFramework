using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

namespace XFramework.Portal.Services;

// The cookie holds a session handle; encrypted tickets keep circuit rotations available
// to other tabs and after a restart. Retention follows the authentication ticket's expiry.
public sealed class PortalAuthenticationTicketStore(
    IDistributedCache cache,
    IDataProtectionProvider protection,
    TimeProvider clock) : ITicketStore
{
    private readonly IDataProtector _protector = protection.CreateProtector("Portal.AuthenticationTickets.v1");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        if (ticket.AuthenticationScheme != PortalAuthDefaults.AuthenticationScheme ||
            !PortalIdentitySessionValidator.TryReadSessionClaims(ticket.Principal, out _, out _, out var session, out _) ||
            ticket.Properties.ExpiresUtc is not { } expiry || expiry <= clock.GetUtcNow())
            throw new InvalidOperationException("A Portal ticket requires valid session bindings and a future expiry.");

        var key = session.ToString("N");
        await _gate.WaitAsync();
        try { await WriteAsync(key, ticket); }
        finally { _gate.Release(); }
        return key;
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await _gate.WaitAsync();
        try { return await ReadAsync(key); }
        finally { _gate.Release(); }
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await _gate.WaitAsync();
        try
        {
            if (await ReadAsync(key) is { } current && BindingsMatch(current.Principal, ticket.Principal) &&
                ticket.Properties.ExpiresUtc is { } expiry && expiry > clock.GetUtcNow())
            {
                // A late HTTP renewal must preserve tokens already rotated by a circuit.
                await WriteAsync(key, new AuthenticationTicket(
                    current.Principal, ticket.Properties.Clone(), current.AuthenticationScheme));
            }
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAsync(string key)
    {
        await _gate.WaitAsync();
        try { await cache.RemoveAsync(CacheKey(key)); }
        finally { _gate.Release(); }
    }

    public async Task<ClaimsPrincipal?> ReadPrincipalAsync(ClaimsPrincipal principal)
    {
        await _gate.WaitAsync();
        try { return (await ReadBoundTicketAsync(principal))?.Principal; }
        finally { _gate.Release(); }
    }

    public async Task<bool> UpdateTokensAsync(
        ClaimsPrincipal principal, string expectedRefreshToken, PortalActorTokenPair tokens)
    {
        await _gate.WaitAsync();
        try
        {
            var ticket = await ReadBoundTicketAsync(principal);
            if (ticket is null ||
                ticket.Principal.FindFirst(PortalAuthClaims.SessionId)?.Value != tokens.SessionId.ToString())
                return false;

            var currentRefresh = ticket.Principal.FindFirst(PortalAuthClaims.RefreshToken)?.Value;
            if (currentRefresh == tokens.RefreshToken) return true;
            if (currentRefresh != expectedRefreshToken) return false;

            var identity = (ClaimsIdentity)ticket.Principal.Identity!;
            ReplaceClaim(identity, PortalAuthClaims.ActorAccessToken, tokens.AccessToken);
            ReplaceClaim(identity, PortalAuthClaims.RefreshToken, tokens.RefreshToken);
            await WriteAsync(tokens.SessionId.ToString("N"), ticket);
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task<AuthenticationTicket?> ReadBoundTicketAsync(ClaimsPrincipal principal)
    {
        if (!PortalIdentitySessionValidator.TryReadSessionClaims(principal, out _, out _, out var session, out _))
            return null;
        var ticket = await ReadAsync(session.ToString("N"));
        return ticket is not null && BindingsMatch(ticket.Principal, principal) ? ticket : null;
    }

    private async Task<AuthenticationTicket?> ReadAsync(string key)
    {
        if (!Guid.TryParseExact(key, "N", out var session) || session == Guid.Empty) return null;
        var payload = await cache.GetAsync(CacheKey(key));
        if (payload is null) return null;
        AuthenticationTicket? ticket;
        try { ticket = TicketSerializer.Default.Deserialize(_protector.CreateProtector(key).Unprotect(payload)); }
        catch (CryptographicException) { return null; }
        if (ticket?.AuthenticationScheme == PortalAuthDefaults.AuthenticationScheme &&
            PortalIdentitySessionValidator.TryReadSessionClaims(ticket.Principal, out _, out _, out var storedSession, out _) &&
            storedSession == session && ticket.Properties.ExpiresUtc > clock.GetUtcNow())
            return ticket;
        await cache.RemoveAsync(CacheKey(key));
        return null;
    }

    private async Task WriteAsync(string key, AuthenticationTicket ticket)
    {
        var remaining = ticket.Properties.ExpiresUtc!.Value - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return;
        await cache.SetAsync(CacheKey(key), _protector.CreateProtector(key).Protect(TicketSerializer.Default.Serialize(ticket)),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });
    }

    private static string CacheKey(string key) => $"portal:authentication:session:{key}";

    private static bool BindingsMatch(ClaimsPrincipal current, ClaimsPrincipal candidate) =>
        PortalIdentitySessionValidator.TryReadSessionClaims(current, out var tenant, out var credential, out var session, out var role) &&
        PortalIdentitySessionValidator.TryReadSessionClaims(candidate, out var otherTenant, out var otherCredential, out var otherSession, out var otherRole) &&
        tenant == otherTenant && credential == otherCredential && session == otherSession && role == otherRole;

    private static void ReplaceClaim(ClaimsIdentity identity, string type, string value)
    {
        foreach (var claim in identity.FindAll(type).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(type, value));
    }
}
