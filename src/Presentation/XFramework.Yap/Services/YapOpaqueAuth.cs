using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Yap.Services;

public static class YapOpaqueAuth
{
    public static void MapYapOpaqueAuth(this WebApplication app)
    {
        app.MapPost("/api/auth/opaque", async (OpaqueAuthRequest request, HttpContext context,
            IAntiforgery antiforgery, IConfiguration configuration, IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, YapSessions sessions, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = "Refresh the page and try again." }); }
            if (!Guid.TryParse(configuration["Yap:TenantId"], out var tenant) || !Guid.TryParse(configuration["Yap:RoleId"], out var role))
                return Results.StatusCode(503);
            request.RoleId = role;
            request.Metadata = new RequestMetadata { RequestedTenantId = tenant, RequestId = Guid.NewGuid(), OperationName = "Yap OPAQUE authentication" };
            CommunicationsChatActor? actor = null;
            if (request.Stage is "status" or "enroll-start" or "change-start" or "enroll-verify" or "enroll-finish"
                && context.User.Identity?.IsAuthenticated == true)
            {
                try { actor = await actors.GetCurrentActorAsync(ct); }
                catch (UnauthorizedAccessException) { /* The server still requires a valid actor for existing-account enrollment. */ }
            }
            using var token = actor is null ? null : tokens.Push(actor.AccessToken!);
            var result = await identity.OpaqueAuth(request, ct);
            if (!result.IsSuccess || result.Response is null)
                return Results.Json(new { error = result.Message }, statusCode: (int)result.HttpStatusCode);
            var response = result.Response;
            if (response.Authentication is { } authenticated)
            {
                if (authenticated.Credential?.TenantId != tenant) return Results.StatusCode(403);
                var principal = await sessions.CreateAsync(authenticated, ct);
                await context.SignInAsync(YapAuth.Scheme, principal,
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
                response.Authentication = null;
            }
            return Results.Ok(response);
        }).AllowAnonymous().WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2200000));
    }
}
