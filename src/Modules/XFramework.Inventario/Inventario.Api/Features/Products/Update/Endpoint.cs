using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace Inventario.Api.Features.Products.Update;

public static class UpdateProductEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/products/{id:guid}", Handle)
            .WithName("UpdateProduct")
            .WithTags("Products")
            .WithSummary("Update an existing product")
            .WithDescription("Updates catalog fields for a product and invalidates the cache")
            .Produces<ProductResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<Results<Ok<ProductResponse>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        Guid id,
        UpdateProductRequest request,
        HttpContext httpContext,
        IValidator<UpdateProductRequest> validator,
        IHttpTrustedInvocationAuthorizer invocationAuthorizer,
        ITrustedInvocationFeatureGate featureGate,
        ProductService productService,
        CancellationToken ct)
    {
        request.Metadata ??= new RequestMetadata();
        request.Metadata.IpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        request.Metadata.UserAgent = httpContext.Request.Headers.UserAgent.ToString();

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
        {
            return TypedResults.Problem(
                detail: invocationResult.Error,
                statusCode: invocationResult.StatusCode);
        }

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/products/{id:guid}",
            HttpMethods.Put,
            null,
            ct);
        if (!featureResult.IsSuccess)
        {
            return TypedResults.Problem(
                detail: featureResult.Message,
                statusCode: featureResult.StatusCode);
        }

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(static e => e.PropertyName)
                .ToDictionary(static g => g.Key, static g => g.Select(static e => e.ErrorMessage).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        var result = await productService.UpdateAsync(id, request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
            };
        }

        var response = ProductResponse.FromProduct(result.Data!);
        return TypedResults.Ok(response);
    }
}
