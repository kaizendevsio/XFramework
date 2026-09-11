using FluentValidation;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class IdentityAddressTypeValidator : AbstractValidator<IdentityAddressType>
{
    public IdentityAddressTypeValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
}
