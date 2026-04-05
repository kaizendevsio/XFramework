namespace Community.Api.Features.Connections.Delete;

/// <summary>
/// Validator for DeleteConnectionRequest
/// </summary>
public sealed class DeleteConnectionValidator : AbstractValidator<DeleteConnectionRequest>
{
    public DeleteConnectionValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Connection ID is required");

        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");
    }
}
