using FluentValidation;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Core.Validations.Common;

public class RequestServerValidator : AbstractValidator<RequestServer>
{
    public RequestServerValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("Application Id is Required");
    }
}