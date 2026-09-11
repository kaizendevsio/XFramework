using Communications.Integration.Clients;
using Microsoft.AspNetCore.Components.Authorization;

namespace Yap.Services;

public sealed class YapActorProvider(AuthenticationStateProvider authentication, YapSessions sessions)
    : ICommunicationsChatActorProvider
{
    public async ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default)
    {
        var state = await authentication.GetAuthenticationStateAsync().WaitAsync(ct);
        return await sessions.GetActorAsync(state.User, ct);
    }
}
