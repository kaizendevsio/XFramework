using System.Security.Claims;
using Bolt.Server;

namespace Bolt.Tests;

// Explicitly isolated policy for protocol tests. Production must authorize tenant and membership.
internal sealed class MediaTestAuthorization : IBoltCallAuthorizer
{
    public static MediaTestAuthorization Instance { get; } = new();
    public ValueTask<bool> AuthorizeAsync(BoltCallAuthorizationContext context, CancellationToken ct = default)
        => ValueTask.FromResult(true);

    public static ClaimsPrincipal User(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "media-test"));
}
