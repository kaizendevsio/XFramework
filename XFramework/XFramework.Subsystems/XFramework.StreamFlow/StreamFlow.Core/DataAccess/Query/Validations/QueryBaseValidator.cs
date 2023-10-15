using FluentValidation;
using StreamFlow.Core.Validations.Common;

namespace StreamFlow.Core.DataAccess.Query.Validations;

public class QueryBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerValidator RequestServerValidator { get; set; }

    public QueryBaseValidator()
    {
        RequestServerValidator = new RequestServerValidator();
    }
}