using FluentValidation;
using XFramework.Client.Shared.Entity.Models.Requests.Session;

namespace XFramework.Client.Shared.Entity.Validations.Session;

public class SmsVerificationRequestValidator : AbstractValidator<VerificationRequest>
{
    public SmsVerificationRequestValidator()
    {
        RuleFor(i => i.OtpCode)
            .NotEmpty();
    }
}