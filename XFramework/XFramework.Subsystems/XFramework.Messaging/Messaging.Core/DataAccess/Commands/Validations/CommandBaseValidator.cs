using Messaging.Core.Validations.Common;
using FluentValidation;

namespace Messaging.Core.DataAccess.Commands.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }
        
    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}