using FluentValidation;
using IdentityServer.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Entity.Validations.Affiliate;

public class CreateAffiliateSubscriptionValidator : AbstractValidator<CreateAffiliateSubscriptionRequest>
{
    public CreateAffiliateSubscriptionValidator()
    {
        RuleFor(i => i.Value).NotEmpty().WithMessage("Phone number is required");
    }
}