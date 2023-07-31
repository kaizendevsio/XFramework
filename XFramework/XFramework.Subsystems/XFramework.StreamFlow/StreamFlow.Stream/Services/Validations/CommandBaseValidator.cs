using FluentValidation;
using StreamFlow.Core.Validations.Common;

namespace StreamFlow.Stream.Services.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }

    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}