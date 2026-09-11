using FluentValidation;

namespace Wallets.Api.Features.ReferenceData;

public sealed class CurrencyTypeValidator : AbstractValidator<CurrencyType>
{
    public CurrencyTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.CurrencyIsoCode3).MaximumLength(4);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
