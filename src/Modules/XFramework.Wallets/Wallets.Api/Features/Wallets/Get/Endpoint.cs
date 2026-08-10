using Microsoft.AspNetCore.Http.HttpResults;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Security;

namespace Wallets.Api.Features.Wallets.Get;

/// <summary>
/// Get Wallet endpoint
/// </summary>
public static class GetWalletEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/wallets/{walletId:guid}", Handle)
            .WithName("GetWallet")
            .WithTags("Wallets")
            .RequireAuthorization()
            .ExcludeFromDescription();
    }

    public static async Task<IResult> Handle(
        [FromRoute] Guid walletId,
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
                RequireServiceIdentity = false,
                RequiredActorCapabilities = [WalletAuthorizationCapabilities.View]
            },
            ct);
        if (!invocationResult.IsSuccess)
            return TypedResults.Problem(detail: invocationResult.Error, statusCode: invocationResult.StatusCode);

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/wallets/{walletId:guid}",
            HttpMethods.Get,
            null,
            ct);
        if (!featureResult.IsSuccess)
            return TypedResults.Problem(detail: featureResult.Message, statusCode: featureResult.StatusCode);

        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Invalid wallet tenant context",
                detail: contextResult.Message,
                statusCode: contextResult.StatusCode);
        }

        var result = await walletService.GetWalletAsync(walletId, contextResult.Data!.TenantId, ct);

        if (!result.IsSuccess)
        {
            if (result.StatusCode == 404)
                return TypedResults.NotFound();

            return TypedResults.Problem(
                title: "Error retrieving wallet",
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        if (!contextResult.Data.IsPrivilegedActor &&
            contextResult.Data.ActorCredentialId != result.Data!.CredentialId)
        {
            return TypedResults.Problem(
                title: "Wallet access denied",
                detail: "Actor cannot access the requested wallet",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var response = WalletResponse.FromWallet(result.Data!);
        return TypedResults.Ok(response);
    }
}
