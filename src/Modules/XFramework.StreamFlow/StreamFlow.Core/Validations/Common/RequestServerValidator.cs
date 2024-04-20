using FluentValidation;
using XFramework.Domain.Shared.BusinessObjects;

namespace StreamFlow.Core.Validations.Common;

public class RequestServerValidator : AbstractValidator<RequestMetadata>
{
    public RequestServerValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Application Id is Required");
    }
}