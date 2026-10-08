using Communications.Integration.Clients;

namespace Yap.Services;

public sealed class YapActorProvider(IHttpContextAccessor context, YapSessions sessions, YapTenants tenants)
    : ICommunicationsChatActorProvider
{
    public async ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default)
    {
        var http = context.HttpContext;
        if (http is not null && !tenants.Matches(http, http.User))
            throw new UnauthorizedAccessException("This sign-in belongs to another workspace.");
        var user = http?.User;
        return user is null ? null : await sessions.GetActorAsync(user, ct);
    }
}
