using FluentValidation;
using Wallets.Core.Validations.Common;

namespace Wallets.Core.DataAccess.Query.Validations;

public class QueryBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }

    public QueryBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}