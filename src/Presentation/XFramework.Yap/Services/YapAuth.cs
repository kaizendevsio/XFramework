using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;

namespace Yap.Services;

public static class YapAuth
{
    public const string Scheme = "YapCookie";
    public const string SessionClaim = "yap_session";
    public const string TenantClaim = "yap_tenant";

    public static void MapYapAuth(this WebApplication app)
    {
        app.MapPost("/auth/login", LoginAsync).AllowAnonymous();
        app.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(HttpContext context, IAntiforgery antiforgery,
        IConfiguration configuration, IIdentityServerServiceWrapper identity, YapSessions sessions,
        ILogger<YapSessions> logger, CancellationToken ct)
    {
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Results.BadRequest("Refresh the page and try again."); }
        var form = await context.Request.ReadFormAsync(ct);
        var username = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        if (username.Length is 0 or > 150 || password.Length is 0 or > 256)
            return Results.Redirect("/login?error=credentials");
        if (!Guid.TryParse(configuration["Yap:TenantId"], out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(configuration["Yap:RoleId"], out var role) || role == Guid.Empty)
            return Results.Redirect("/login?error=setup");
        try
        {
            var response = await identity.AuthenticateIdentity(new AuthenticateIdentityRequest
            {
                UserName = username, Password = password, RoleId = role,
                AuthorizationType = AuthorizationType.Username, GenerateToken = true,
                Metadata = new RequestMetadata { RequestedTenantId = tenant, RequestId = Guid.NewGuid(), OperationName = "Yap login" }
            }, ct);
            if (!response.IsSuccess || response.Response?.Credential?.TenantId != tenant)
                return Results.Redirect("/login?error=credentials");
            var principal = sessions.Create(response.Response);
            await context.SignInAsync(Scheme, principal,
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
            return Results.Redirect("/");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yap authentication service could not complete sign-in.");
            return Results.Redirect("/login?error=unavailable");
        }
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAntiforgery antiforgery,
        YapSessions sessions, ILogger<YapSessions> logger, CancellationToken ct)
    {
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Results.BadRequest("Refresh the page and try again."); }
        try { await sessions.RevokeAsync(context.User, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Yap could not revoke the upstream session during sign-out."); }
        finally { await context.SignOutAsync(Scheme); }
        return Results.Redirect("/login");
    }
}
