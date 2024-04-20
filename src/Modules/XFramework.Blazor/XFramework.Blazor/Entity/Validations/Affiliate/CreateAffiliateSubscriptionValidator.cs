using FluentValidation;
using IdentityServer.Domain.Shared.Contracts.Requests;

namespace XFramework.Blazor.Entity.Validations.Affiliate;

public class CreateAffiliateSubscriptionValidator : AbstractValidator<CreateAffiliateSubscriptionRequest>
{
    public CreateAffiliateSubscriptionValidator()
    {
        RuleFor(i => i.Value).NotEmpty().WithMessage("Phone number is required");
    }
}