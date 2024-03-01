using FluentValidation;
using StreamFlow.Core.Validations.Common;

namespace StreamFlow.Stream.Services.Validations;

public class CommandBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerValidator RequestServerValidator { get; set; }

    public CommandBaseValidator()
    {
        RequestServerValidator = new RequestServerValidator();
    }
}