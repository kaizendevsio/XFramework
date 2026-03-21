using Microsoft.AspNetCore.Http.HttpResults;
using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Api.Features.Sms.GetPending;

/// <summary>
/// Get pending SMS messages endpoint
/// </summary>
public static class GetPendingSmsMessagesEndpoint
{
    public static void MapGetPendingSmsMessages(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sms/messages/pending/{agentClusterId:guid}", Handle)
            .WithName("GetPendingSmsMessages")
            .WithTags("SMS")
            .WithOpenApi(op =>
            {
                op.Summary = "Get pending SMS messages";
                op.Description = "Gets a list of pending SMS messages for a specific agent cluster";
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
        var request = new GetPendingSmsMessageListRequest
        {
            AgentClusterId = agentClusterId
        };

        var result = await smsService.GetPendingSmsMessagesAsync(request, ct);

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