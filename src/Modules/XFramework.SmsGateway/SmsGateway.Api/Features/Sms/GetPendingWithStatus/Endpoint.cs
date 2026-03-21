using Microsoft.AspNetCore.Http.HttpResults;
using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Api.Features.Sms.GetPendingWithStatus;

/// <summary>
/// Get pending SMS messages and update status to Processing endpoint.
/// This replaces the legacy SmsGatewayNodeController.List endpoint.
/// </summary>
public static class GetPendingWithStatusUpdateEndpoint
{
    public static void MapGetPendingWithStatusUpdate(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/SmsGatewayNode/List/{agentClusterId:guid}", Handle)
            .WithName("GetPendingWithStatusUpdate")
            .WithTags("SMS")
            .WithOpenApi(op =>
            {
                op.Summary = "Get pending messages and set to processing";
                op.Description = "Gets pending SMS messages and updates their status to Processing. This endpoint is used by SMS gateway nodes.";
                return op;
            })
            .Produces<List<SmsNodeJob>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<List<SmsNodeJob>>, ProblemHttpResult>> Handle(
        Guid agentClusterId,
        ISmsService smsService,
        CancellationToken ct)
    {
        var result = await smsService.GetPendingWithStatusUpdateAsync(agentClusterId, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error retrieving pending SMS messages",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok(result.Data!);
    }
}