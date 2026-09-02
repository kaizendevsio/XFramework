using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace XFramework.Portal.Services;

public sealed class PortalRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    public static readonly TimeSpan CircuitRevalidationInterval = TimeSpan.FromMinutes(1);

    protected override TimeSpan RevalidationInterval => CircuitRevalidationInterval;

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var validator = scope.ServiceProvider.GetRequiredService<PortalIdentitySessionValidator>();
        var validation = await validator.ValidateAndRefreshAsync(
            authenticationState.User,
            cancellationToken);
        return validation.IsValid;
    }
}
