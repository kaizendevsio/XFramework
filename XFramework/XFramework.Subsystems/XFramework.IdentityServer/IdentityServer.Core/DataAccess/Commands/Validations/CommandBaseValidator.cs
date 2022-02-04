using FluentValidation;
using IdentityServer.Core.Validations.Common;

namespace IdentityServer.Core.DataAccess.Commands.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }
        
    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}