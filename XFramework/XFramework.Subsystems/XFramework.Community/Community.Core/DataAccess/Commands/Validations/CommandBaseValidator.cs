using Community.Core.Validations.Common;
using FluentValidation;

namespace Community.Core.DataAccess.Commands.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }
        
    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}