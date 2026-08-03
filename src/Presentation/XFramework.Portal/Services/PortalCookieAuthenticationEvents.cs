using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace XFramework.Portal.Services;

public sealed class PortalCookieAuthenticationEvents(
    PortalIdentitySessionValidator sessionValidator,
    ILogger<PortalCookieAuthenticationEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (await sessionValidator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
        {
            return;
        }

        logger.LogWarning("Rejecting a Portal cookie because its IdentityServer session is no longer valid.");
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(PortalAuthDefaults.AuthenticationScheme);
    }
}
