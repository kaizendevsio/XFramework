﻿using FluentValidation;

namespace PaymentGateways.Core.Validations.Common;

public class RequestServerBoValidator : AbstractValidator<RequestServerBO>
{
    public RequestServerBoValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("Application Id is Required");
    }
}