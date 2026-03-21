using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using XFramework.Domain.Shared.Contracts;

namespace Community.Api.Features.Connections.GetList;

/// <summary>
/// Get Connection List endpoint
/// </summary>
public static class GetConnectionListEndpoint
{
    public static void MapGetConnectionList(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/community/connections", Handle)
            .WithName("GetConnectionList")
            .WithTags("Community Connections")
            .WithOpenApi(op =>
            {
                op.Summary = "Get a list of connections for an identity";
                op.Description = "Retrieves connections for a community identity filtered by connection type";
                return op;
            })
            .Produces<List<CommunityConnection>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<List<CommunityConnection>>, NotFound<string>, ProblemHttpResult>> Handle(
        [FromQuery] Guid connectionTypeId,
        [FromQuery] Guid communityIdentityId,
        [FromQuery] int limit,
        ICommunityService communityService,
        CancellationToken ct)
    {
        // Set defaults
        limit = limit <= 0 ? 10 : Math.Min(limit, 100); // Max 100 items

        var request = new GetCommunityConnectionListRequest
        {
            ConnectionTypeId = connectionTypeId,
            CommunityIdentityId = communityIdentityId,
            Limit = limit
        };

        var result = await communityService.GetConnectionListAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode == 404
                ? TypedResults.NotFound(result.Message)
                : TypedResults.Problem(
                    title: "Error retrieving connections",
                    detail: result.Message,
                    statusCode: result.StatusCode
                );
        }

        return TypedResults.Ok(result.Data);
    }
}