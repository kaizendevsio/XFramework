using Communications.Integration.Clients;

namespace Yap.Services;

public sealed class YapActorProvider(IHttpContextAccessor context, YapSessions sessions)
    : ICommunicationsChatActorProvider
{
    public async ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default)
    {
        var user = context.HttpContext?.User;
        return user is null ? null : await sessions.GetActorAsync(user, ct);
    }
}
