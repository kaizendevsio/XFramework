using FluentValidation;
using StreamFlow.Core.Validations.Common;

namespace StreamFlow.Core.DataAccess.Query.Validations;

public class QueryBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }

    public QueryBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}