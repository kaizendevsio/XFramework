using FluentValidation;
using Wallets.Core.Validations.Common;

namespace Wallets.Core.DataAccess.Commands.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }

    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}