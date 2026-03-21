using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using XFramework.Domain.Shared.BusinessObjects;

namespace SmsGateway.Api.Features.Sms.CreateReceived;

/// <summary>
/// Create received SMS message endpoint
/// </summary>
public static class CreateMessageReceivedEndpoint
{
    public static void MapCreateMessageReceived(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sms/messages/received", Handle)
            .WithName("CreateMessageReceived")
            .WithTags("SMS")
            .WithOpenApi(op =>
            {
                op.Summary = "Create received message record";
                op.Description = "Creates a record of a received SMS message";
                return op;
            })
            .Produces<CmdResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<CmdResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        CreateMessageReceivedRequest request,
        ISmsService smsService,
        IValidator<CreateMessageReceivedRequest> validator,
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
        var result = await smsService.CreateMessageReceivedAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error creating message received record",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok(result.Data!);
    }
}