using FluentValidation;
using PaymentGateways.Core.Validations.Common;

namespace PaymentGateways.Core.DataAccess.Query.Validations;

public class QueryBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }

    public QueryBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}