using SmsGateway.Core.Validations.Common;
using FluentValidation;

namespace SmsGateway.Core.DataAccess.Query.Validations;

public class QueryBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }
        
    public QueryBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}