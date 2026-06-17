using Microsoft.AspNetCore.Authentication;
using ControlPanel.Server.Services;

namespace ControlPanel.Server.Extensions;

public static class ControlPanelAuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapControlPanelAuthEndpoints(this IEndpointRouteBuilder endpoints)
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
        ControlPanelAuthService authService,
        CancellationToken ct)
    {
        var form = await context.Request.ReadFormAsync(ct);
        var returnUrl = form["returnUrl"].ToString();
        var userName = form["username"].ToString();
        var password = form["password"].ToString();
        var rememberMe = string.Equals(form["rememberMe"].ToString(), "true", StringComparison.OrdinalIgnoreCase);

        var result = await authService.AuthenticateAsync(userName, password, rememberMe, context, ct);
        if (!result.IsSuccess || result.Principal is null)
        {
            return Results.Redirect(BuildLoginUrl(result.Error, returnUrl));
        }

        await context.SignInAsync(
            ControlPanelAuthDefaults.AuthenticationScheme,
            result.Principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(rememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(12))
            });

        return Results.Redirect(GetSafeReturnUrl(returnUrl));
    }

    private static async Task<IResult> Logout(HttpContext context)
    {
        await context.SignOutAsync(ControlPanelAuthDefaults.AuthenticationScheme);
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
