using Microsoft.AspNetCore.Http.HttpResults;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Security;

namespace Wallets.Api.Features.Wallets.GetByCredential;

/// <summary>
/// Get Wallets by Credential endpoint
/// </summary>
public static class GetWalletsByCredentialEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/wallets/credential/{credentialId:guid}", Handle)
            .WithName("GetWalletsByCredential")
            .WithTags("Wallets")
            .RequireAuthorization()
            .ExcludeFromDescription();
    }

    public static async Task<IResult> Handle(
        [FromRoute] Guid credentialId,
        [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId,
        HttpContext httpContext,
        [FromServices] IHttpTrustedInvocationAuthorizer invocationAuthorizer,
        [FromServices] ITrustedInvocationFeatureGate featureGate,
        [FromServices] IWalletRequestContextResolver contextResolver,
        [FromServices] IWalletOperationsService walletService,
        CancellationToken ct)
    {
        var request = new RequestBase
        {
            Metadata = new RequestMetadata { RequestedTenantId = tenantId }
        };
        var invocationResult = await invocationAuthorizer.AuthorizeAsync(
            httpContext.Request.Headers.Authorization.ToString(),
            httpContext.Request.Headers["X-XFramework-Service-Authorization"].ToString(),
            request.Metadata,
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Required,
                TenantAccessMode = TenantAccessMode.ActorTenant,
                RequireServiceIdentity = false
            },
            ct);
        if (!invocationResult.IsSuccess)
            return TypedResults.Problem(detail: invocationResult.Error, statusCode: invocationResult.StatusCode);

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/wallets/credential/{credentialId:guid}",
            HttpMethods.Get,
            null,
            ct);
        if (!featureResult.IsSuccess)
            return TypedResults.Problem(detail: featureResult.Message, statusCode: featureResult.StatusCode);

        var contextResult = contextResolver.Resolve(request, credentialId);
        if (!contextResult.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Invalid wallet tenant context",
                detail: contextResult.Message,
                statusCode: contextResult.StatusCode);
        }

        var result = await walletService.GetWalletsByCredentialAsync(credentialId, contextResult.Data!.TenantId, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error retrieving wallets",
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        var response = result.Data!.Select(WalletResponse.FromWallet).ToList();
        return TypedResults.Ok(response);
    }
}
