using FluentValidation;
using RBS.Core.Validations.Common;

namespace RBS.Core.DataAccess.Commands.Validations
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