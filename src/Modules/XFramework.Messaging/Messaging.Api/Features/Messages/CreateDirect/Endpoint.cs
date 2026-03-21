using FluentValidation;
using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.Contracts;

namespace Messaging.Api.Features.Messages.CreateDirect;

/// <summary>
/// Create Direct Message endpoint
/// </summary>
public static class CreateDirectMessageEndpoint
{
    public static void MapCreateDirectMessage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/messages/direct", Handle)
            .WithName("CreateDirectMessage")
            .WithTags("Messages")
            .WithOpenApi(op =>
            {
                op.Summary = "Create and send a direct message";
                op.Description = "Creates and sends a direct message (SMS/Email) to the specified recipient";
                return op;
            })
            .Produces<CmdResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Created<CmdResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        CreateDirectMessageRequest request,
        IMessagingService messagingService,
        IValidator<CreateDirectMessageRequest> validator,
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
        var result = await messagingService.CreateDirectMessageAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error creating direct message",
                detail: result.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        return TypedResults.Created("/api/messages/direct", result.Data);
    }
}