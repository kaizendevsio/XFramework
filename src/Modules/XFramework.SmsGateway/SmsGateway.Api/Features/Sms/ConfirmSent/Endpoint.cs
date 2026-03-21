using Microsoft.AspNetCore.Http.HttpResults;
using SmsGateway.Api.Services;
using XFramework.Domain.Shared.BusinessObjects;

namespace SmsGateway.Api.Features.Sms.ConfirmSent;

/// <summary>
/// Confirm SMS message sent endpoint
/// </summary>
public static class ConfirmMessageSentEndpoint
{
    public static void MapConfirmMessageSent(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/sms/messages/{id:guid}/sent", Handle)
            .WithName("ConfirmMessageSent")
            .WithTags("SMS")
            .WithOpenApi(op =>
            {
                op.Summary = "Confirm message sent";
                op.Description = "Confirms that an SMS message has been sent successfully";
                return op;
            })
            .Produces<CmdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<CmdResponse>, NotFound, ProblemHttpResult>> Handle(
        Guid id,
        ISmsService smsService,
        CancellationToken ct)
    {
        var result = await smsService.ConfirmMessageSentAsync(id, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode == 404
                ? TypedResults.NotFound()
                : TypedResults.Problem(
                    title: "Error confirming message sent",
                    detail: result.Message,
                    statusCode: result.StatusCode
                );
        }

        return TypedResults.Ok(result.Data!);
    }
}