using FluentValidation;
using SmsGateway.Core.DataAccess.Commands.Entity.Sms;

namespace SmsGateway.Core.Validations.Sms;

public class CreateSmsMessageCmdValidator : AbstractValidator<CreateSmsMessageCmd>
{
    public CreateSmsMessageCmdValidator()
    {
        RuleFor(i => i.Recipient)
            .NotEmpty();
        RuleFor(i => i.Message)
            .NotEmpty();
        RuleFor(i => i.Sender)
            .NotEmpty();
    }
}