using Microsoft.AspNetCore.Http.HttpResults;
using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Api.Features.Sms.GetScheduled;

/// <summary>
/// Get scheduled SMS messages endpoint
/// </summary>
public static class GetScheduledSmsMessagesEndpoint
{
    public static void MapGetScheduledSmsMessages(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sms/messages/scheduled/{agentClusterId:guid}", Handle)
            .WithName("GetScheduledSmsMessages")
            .WithTags("SMS")
            .WithOpenApi(op =>
            {
                op.Summary = "Get scheduled SMS messages";
                op.Description = "Gets a list of scheduled SMS messages for a specific agent cluster";
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
        var request = new GetScheduledSmsMessageListRequest
        {
            AgentClusterId = agentClusterId
        };

        var result = await smsService.GetScheduledSmsMessagesAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error retrieving scheduled SMS messages",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok(result.Data!);
    }
}