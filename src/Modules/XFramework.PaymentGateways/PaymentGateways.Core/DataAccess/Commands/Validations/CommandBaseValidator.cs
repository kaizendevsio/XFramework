using FluentValidation;
using PaymentGateways.Core.Validations.Common;

namespace PaymentGateways.Core.DataAccess.Commands.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }

    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}