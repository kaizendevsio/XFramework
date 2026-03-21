using FluentValidation;
using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.BusinessObjects;

namespace Community.Api.Features.CommunityIdentities.Create;

/// <summary>
/// Create Community Identity endpoint
/// </summary>
public static class CreateCommunityIdentityEndpoint
{
    public static void MapCreateCommunityIdentity(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/community/identities", Handle)
            .WithName("CreateCommunityIdentity")
            .WithTags("Community Identities")
            .WithOpenApi(op =>
            {
                op.Summary = "Create a new community identity";
                op.Description = "Creates a new community identity for a credential";
                return op;
            })
            .Produces<CmdResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Accepted<CmdResponse>, NotFound<string>, ValidationProblem, ProblemHttpResult>> Handle(
        CreateCommunityIdentityRequest request,
        ICommunityService communityService,
        IValidator<CreateCommunityIdentityRequest> validator,
        CancellationToken ct)
    {
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
        var result = await communityService.CreateCommunityIdentityAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode == 404
                ? TypedResults.NotFound(result.Message)
                : TypedResults.Problem(
                    title: "Error creating community identity",
                    detail: result.Message,
                    statusCode: result.StatusCode
                );
        }

        return TypedResults.Accepted("/api/community/identities", result.Data);
    }
}