using FluentValidation;
using Records.Core.DataAccess.Commands.Entity;
using Records.Core.Validations.Common;

namespace Records.Core.DataAccess.Commands.Validations
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