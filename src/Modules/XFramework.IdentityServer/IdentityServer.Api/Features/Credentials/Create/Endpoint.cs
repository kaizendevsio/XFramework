using FluentValidation;
using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using CreateRequest = XFramework.Domain.Shared.Contracts.Requests.Create<XFramework.Domain.Shared.Contracts.IdentityCredential>;

namespace IdentityServer.Api.Features.Credentials.Create;

/// <summary>
/// Create credential endpoint
/// </summary>
public static class CreateCredentialEndpoint
{
    public static void MapCreateCredential(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/credentials", Handle)
            .WithName("CreateCredential")
            .WithTags("Credentials")
            .WithOpenApi(op =>
            {
                op.Summary = "Create a new identity credential";
                op.Description = "Creates a new identity credential with BCrypt password hashing (workFactor 11).";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857 — IdentityCredential has circular navigation properties
    }

    private static async Task<Results<Created<IdentityCredential>, ValidationProblem, ProblemHttpResult>> Handle(
        CreateRequest request,
        IAuthService authService,
        IValidator<CreateRequest> validator,
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

        var result = await authService.CreateCredentialAsync(request, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error creating credential",
                detail: result.Message,
                statusCode: result.StatusCode
            );
        }

        return TypedResults.Created($"/api/credentials/{result.Data!.Id}", result.Data);
    }
}

/// <summary>
/// Validator for Create IdentityCredential request
/// </summary>
public class CreateCredentialRequestValidator : AbstractValidator<CreateRequest>
{
    public CreateCredentialRequestValidator()
    {
        RuleFor(x => x.Model.UserName)
            .NotEmpty().WithMessage("Username is required");

        RuleFor(x => x.Model.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.Model.IdentityInfoId)
            .NotEmpty().WithMessage("Identity Info ID is required");
    }
}