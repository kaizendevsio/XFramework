using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.System;

namespace XFramework.Identity.Shared.Entity.Validations
{
    public class SystemLogVmValidator: AbstractValidator<SystemLogVm>
    {
        public SystemLogVmValidator(){
            RuleFor(x => x.Initiator).NotEmpty()
                .WithMessage("Enter initiator details");
            RuleFor(x => x.Severity).NotEmpty()
                .WithMessage("Specify severity");
            RuleFor(x => x.Message).NotEmpty()
                .WithMessage("Enter message details");
            RuleFor(x => x.CreatedAt).NotEmpty()
                .WithMessage("Enter date created");
        }
    }
}