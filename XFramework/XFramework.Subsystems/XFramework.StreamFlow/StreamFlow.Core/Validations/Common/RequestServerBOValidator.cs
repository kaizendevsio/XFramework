﻿using FluentValidation;
using IdentityServer.Domain.BusinessObjects;

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