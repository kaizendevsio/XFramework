using FluentValidation;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;

namespace SmsGateway.Core.Validations.Sms;

public class CreateSmsMessageCmdValidator : AbstractValidator<CreateSmsMessageRequest>
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