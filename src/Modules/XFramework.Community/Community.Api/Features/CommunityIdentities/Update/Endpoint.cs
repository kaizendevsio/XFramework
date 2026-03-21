using FluentValidation;
using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.BusinessObjects;

namespace Community.Api.Features.CommunityIdentities.Update;

/// <summary>
/// Update Community Identity endpoint
/// </summary>
public static class UpdateCommunityIdentityEndpoint
{
    public static void MapUpdateCommunityIdentity(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/community/identities/{id:guid}", Handle)
            .WithName("UpdateCommunityIdentity")
            .WithTags("Community Identities")
            .WithOpenApi(op =>
            {
                op.Summary = "Update an existing community identity";
                op.Description = "Updates a community identity by ID";
                return op;
            })
            .Produces<CmdResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Accepted<CmdResponse>, NotFound<string>, ValidationProblem, ProblemHttpResult>> Handle(
        Guid id,
        UpdateCommunityIdentityRequest request,
        ICommunityService communityService,
        IValidator<UpdateCommunityIdentityRequest> validator,
        CancellationToken ct)
    {
        // Set the ID from the route
        request = request with { Id = id };

        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            return TypedResults.ValidationProblem(errors);
        }

        // Call service
        var result = await communityService.UpdateCommunityIdentityAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode == 404
                ? TypedResults.NotFound(result.Message)
                : TypedResults.Problem(
                    title: "Error updating community identity",
                    detail: result.Message,
                    statusCode: result.StatusCode
                );
        }

        return TypedResults.Accepted($"/api/community/identities/{id}", result.Data);
    }
}