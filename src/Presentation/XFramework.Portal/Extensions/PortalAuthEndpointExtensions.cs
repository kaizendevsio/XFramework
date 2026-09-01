using Microsoft.AspNetCore.Authentication;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Portal.Services;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Portal.Extensions;

public static class PortalAuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapPortalAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login", (Delegate)Login)
            .AllowAnonymous()
            .DisableAntiforgery();

        endpoints.MapGet("/auth/logout", (Delegate)Logout)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> Login(
        HttpContext context,
        PortalAuthService authService,
        ILogger<PortalAuthService> logger,
        CancellationToken ct)
    {
        var form = await context.Request.ReadFormAsync(ct);
        var returnUrl = form["returnUrl"].ToString();
        var userName = form["username"].ToString();
        var password = form["password"].ToString();
        var rememberMe = string.Equals(form["rememberMe"].ToString(), "true", StringComparison.OrdinalIgnoreCase);

        PortalLoginResult result;
        try
        {
            result = await authService.AuthenticateAsync(userName, password, rememberMe, context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Portal login failed because the authentication dependencies are unavailable.");
            return Results.Redirect(BuildLoginUrl("Sign-in service is temporarily unavailable. Try again shortly.", returnUrl));
        }

        if (!result.IsSuccess || result.Principal is null)
        {
            return Results.Redirect(BuildLoginUrl(result.Error, returnUrl));
        }

        await context.SignInAsync(
            PortalAuthDefaults.AuthenticationScheme,
            result.Principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(rememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(12))
            });

        return Results.Redirect(GetSafeReturnUrl(returnUrl));
    }

    private static async Task<IResult> Logout(
        HttpContext context,
        IIdentityServerServiceWrapper identityServer,
        PortalActorTokenRefreshCoordinator refreshCoordinator,
        ILogger<PortalAuthService> logger)
    {
        Guid? revokedSessionId = null;
        try
        {
            if (!PortalIdentitySessionValidator.TryReadSessionClaims(
                    context.User,
                    out var tenantId,
                    out var credentialId,
                    out var sessionId,
                    out _))
            {
                logger.LogWarning("Portal logout could not revoke the IdentityServer session because required claims were missing.");
            }
            else
            {
                revokedSessionId = sessionId;
                var request = new LogoutRequest
                {
                    SessionId = sessionId,
                    CredentialId = credentialId,
                    Metadata = new RequestMetadata
                    {
                        RequestedTenantId = tenantId,
                        RequestId = Guid.NewGuid(),
                        OperationName = "Portal logout",
                        DeviceName = Environment.MachineName,
                        UserAgent = context.Request.Headers.UserAgent.ToString(),
                        IpAddress = context.Connection.RemoteIpAddress?.ToString()
                    }
                };

                try
                {
                    var result = await identityServer.Logout(request)
                        .WaitAsync(PortalIdentitySessionValidator.ValidationTimeout);
                    if (!result.IsSuccess)
                    {
                        logger.LogWarning(
                            "IdentityServer rejected Portal session revocation. Status={StatusCode}.",
                            (int)result.HttpStatusCode);
                    }
                }
                catch (TimeoutException ex)
                {
                    logger.LogWarning(ex, "IdentityServer session revocation timed out during Portal logout.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "IdentityServer session revocation failed during Portal logout.");
                }
            }
        }
        finally
        {
            if (revokedSessionId is { } sessionId)
                refreshCoordinator.Remove(sessionId);

            await context.SignOutAsync(PortalAuthDefaults.AuthenticationScheme);
        }

        return Results.Redirect("/login");
    }

    private static string BuildLoginUrl(string? error, string? returnUrl)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(error))
        {
            query.Add($"error={Uri.EscapeDataString(error)}");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            query.Add($"returnUrl={Uri.EscapeDataString(GetSafeReturnUrl(returnUrl))}");
        }

        return query.Count == 0 ? "/login" : $"/login?{string.Join("&", query)}";
    }

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//") || returnUrl.StartsWith('\\'))
        {
            return "/";
        }

        return returnUrl;
    }
}
