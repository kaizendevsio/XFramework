using FluentValidation;
using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.Contracts;

namespace Messaging.Api.Features.Messages.UpdateDirect;

/// <summary>
/// Update Direct Message endpoint
/// </summary>
public static class UpdateDirectMessageEndpoint
{
    public static void MapUpdateDirectMessage(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/messages/direct/{id:guid}", Handle)
            .WithName("UpdateDirectMessage")
            .WithTags("Messages")
            .WithOpenApi(op =>
            {
                op.Summary = "Update a direct message";
                op.Description = "Updates a direct message status and delivery timestamps";
                return op;
            })
            .Produces<CmdResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<CmdResponse>, ValidationProblem, ProblemHttpResult>> Handle(
        Guid id,
        UpdateMessageDirectRequest request,
        IMessagingService messagingService,
        IValidator<UpdateMessageDirectRequest> validator,
        CancellationToken ct)
    {
        // Set the ID from route
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
        var result = await messagingService.UpdateMessageDirectAsync(request, ct);

        if (!result.IsSuccess)
        {
            var statusCode = result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status500InternalServerError;
            
            return TypedResults.Problem(
                title: "Error updating direct message",
                detail: result.Message,
                statusCode: statusCode
            );
        }

        return TypedResults.Ok(result.Data);
    }
}