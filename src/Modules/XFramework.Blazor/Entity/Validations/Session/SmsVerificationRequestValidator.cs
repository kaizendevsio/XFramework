using FluentValidation;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Entity.Validations.Session;

public class SmsVerificationRequestValidator : AbstractValidator<VerificationRequest>
{
    public SmsVerificationRequestValidator()
    {
        RuleFor(i => i.OtpCode)
            .NotEmpty();
    }
}