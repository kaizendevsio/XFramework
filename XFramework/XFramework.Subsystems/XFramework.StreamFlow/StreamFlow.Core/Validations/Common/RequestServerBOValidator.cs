using FluentValidation;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Core.Validations.Common
{
    public class RequestServerBoValidator : AbstractValidator<RequestServerBO>
    {
        public RequestServerBoValidator()
        {
            RuleFor(x => x.ApplicationId)
                .NotEmpty()
                .WithMessage("Application Id is Required");
        }
    }
}