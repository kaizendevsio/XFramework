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
        app.MapPost("/auth/register", RegisterAsync).AllowAnonymous();
        app.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();
        app.MapPost("/api/auth/login", LoginAsync).AllowAnonymous();
        app.MapPost("/api/auth/register", RegisterAsync).AllowAnonymous();
        app.MapPost("/api/auth/logout", LogoutAsync).AllowAnonymous();
    }

    private static IResult Redirect(HttpContext context, string location) => context.Request.Path.StartsWithSegments("/api")
        ? Results.Ok(new { redirect = location }) : Results.Redirect(location);

    private static async Task<IResult> RegisterAsync(HttpContext context, IAntiforgery antiforgery,
        IConfiguration configuration, IIdentityServerServiceWrapper identity, ILogger<YapSessions> logger,
        CancellationToken ct)
    {
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { return Results.BadRequest("Refresh the page and try again."); }
        var form = await context.Request.ReadFormAsync(ct);
        var name = form["displayName"].ToString().Trim();
        var username = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        if (name.Length is 0 or > 100 || username.Length is < 3 or > 100 ||
            !System.Text.RegularExpressions.Regex.IsMatch(username, @"\A[a-zA-Z0-9_.-]+\z") ||
            password.Length < 8 || System.Text.Encoding.UTF8.GetByteCount(password) > 72)
            return Redirect(context, "/register?error=validation");
        if (password != form["confirmPassword"].ToString())
            return Redirect(context, "/register?error=mismatch");
        if (!Guid.TryParse(configuration["Yap:TenantId"], out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(configuration["Yap:RoleId"], out var role) || role == Guid.Empty)
            return Redirect(context, "/register?error=disabled");
        try
        {
            var response = await identity.RegisterIdentity(new RegisterIdentityRequest
            {
                DisplayName = name, UserName = username, Password = password,
                Metadata = new RequestMetadata { RequestedTenantId = tenant, RequestId = Guid.NewGuid(), OperationName = "Yap registration" }
            }, ct);
            if (response.IsSuccess && response.Response?.TenantId == tenant && response.Response.RoleId == role)
                return Redirect(context, "/login?registered=true");
            var error = response.HttpStatusCode switch
            {
                System.Net.HttpStatusCode.Conflict => "taken",
                System.Net.HttpStatusCode.BadRequest => "validation",
                System.Net.HttpStatusCode.Forbidden => "disabled",
                System.Net.HttpStatusCode.TooManyRequests => "limited",
                _ => "unavailable"
            };
            return Redirect(context, $"/register?error={error}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Yap registration could not complete ({ErrorType}).", ex.GetType().Name);
            return Redirect(context, "/register?error=unavailable");
        }
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
            return Redirect(context, "/login?error=credentials");
        if (!Guid.TryParse(configuration["Yap:TenantId"], out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(configuration["Yap:RoleId"], out var role) || role == Guid.Empty)
            return Redirect(context, "/login?error=setup");
        try
        {
            var response = await identity.AuthenticateIdentity(new AuthenticateIdentityRequest
            {
                UserName = username, Password = password, RoleId = role,
                AuthorizationType = AuthorizationType.Username, GenerateToken = true,
                Metadata = new RequestMetadata { RequestedTenantId = tenant, RequestId = Guid.NewGuid(), OperationName = "Yap login" }
            }, ct);
            if (!response.IsSuccess || response.Response?.Credential?.TenantId != tenant)
                return Redirect(context, "/login?error=credentials");
            var principal = sessions.Create(response.Response);
            await context.SignInAsync(Scheme, principal,
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
            return Redirect(context, "/");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yap authentication service could not complete sign-in.");
            return Redirect(context, "/login?error=unavailable");
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
        return Redirect(context, "/login");
    }
}
