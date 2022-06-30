using SmsGateway.Core.Validations.Common;
using FluentValidation;

namespace SmsGateway.Core.DataAccess.Commands.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }
        
    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}