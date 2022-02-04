using FluentValidation;
using IdentityServer.Core.Validations.Common;

namespace IdentityServer.Core.DataAccess.Query.Validations;

public class QueryBaseValidator<T> : AbstractValidator<T>
{
    public RequestServerBoValidator RequestServerValidator { get; set; }
        
    public QueryBaseValidator()
    {
        RequestServerValidator = new RequestServerBoValidator();
    }
}