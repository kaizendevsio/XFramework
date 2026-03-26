using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Connections.Create;

/// <summary>
/// Validator for CreateConnectionRequest
/// </summary>
public sealed class CreateConnectionValidator : AbstractValidator<CreateConnectionRequest>
{
    public CreateConnectionValidator()
    {
        RuleFor(x => x.SourceIdentityId)
            .NotEmpty().WithMessage("Source Identity ID is required");

        RuleFor(x => x.TargetIdentityId)
            .NotEmpty().WithMessage("Target Identity ID is required");

        RuleFor(x => x.TypeId)
            .NotEmpty().WithMessage("Connection Type ID is required");

        RuleFor(x => x.SourceIdentityId)
            .NotEqual(x => x.TargetIdentityId)
            .WithMessage("Cannot create a connection to yourself");
    }
}
