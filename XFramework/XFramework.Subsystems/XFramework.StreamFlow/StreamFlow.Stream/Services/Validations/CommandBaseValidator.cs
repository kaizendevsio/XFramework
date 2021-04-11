using FluentValidation;
using StreamFlow.Core.DataAccess.Commands.Entity;
using StreamFlow.Core.Validations.Common;

namespace StreamFlow.Core.DataAccess.Commands.Validations
{
    public class CommandBaseValidator<T> : AbstractValidator<T>
    {
        public RequestServerBoValidator RequestServerValidator { get; set; }

        public CommandBaseValidator()
        {
            RequestServerValidator = new RequestServerBoValidator();
        }
    }
}