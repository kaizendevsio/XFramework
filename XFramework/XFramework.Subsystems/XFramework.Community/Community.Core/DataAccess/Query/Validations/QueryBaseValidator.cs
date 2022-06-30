using Community.Core.Validations.Common;
using FluentValidation;

namespace Community.Core.DataAccess.Query.Validations;

public class QueryBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }
        
    public QueryBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}