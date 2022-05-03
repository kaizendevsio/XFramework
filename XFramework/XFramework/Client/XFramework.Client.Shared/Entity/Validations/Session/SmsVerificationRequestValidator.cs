using FluentValidation;
using XFramework.Client.Shared.Entity.Models.Requests.Session;

namespace XFramework.Client.Shared.Entity.Validations.Session;

public class SmsVerificationRequestValidator : AbstractValidator<SmsVerificationRequest>
{
    public SmsVerificationRequestValidator()
    {
        RuleFor(i => i.OtpCode)
            .NotEmpty();
    }
}