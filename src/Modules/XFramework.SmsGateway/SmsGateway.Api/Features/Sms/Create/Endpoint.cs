using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using XFramework.Domain.Shared.BusinessObjects;

namespace SmsGateway.Api.Features.Sms.Create;

/// <summary>
/// Create SMS message endpoint
/// </summary>
public static class CreateSmsMessageEndpoint
{
    public static void MapCreateSmsMessage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sms/messages", Handle)
            .WithName("CreateSmsMessage")
            .WithTags("SMS")
            .WithOpenApi(op =>
            {
                op.Summary = "Create a new SMS message";
                op.Description = "Creates a new SMS message to be sent via the SMS gateway";
                return op;
            })
            .Produces<CmdResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<CmdResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        CreateSmsMessageRequest request,
        ISmsService smsService,
        IValidator<CreateSmsMessageRequest> validator,
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
        var result = await smsService.CreateSmsMessageAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error creating SMS message",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Ok(result.Data!);
    }
}