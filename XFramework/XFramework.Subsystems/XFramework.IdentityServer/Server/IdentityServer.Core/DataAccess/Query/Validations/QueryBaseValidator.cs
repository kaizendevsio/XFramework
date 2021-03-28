using FluentValidation;
using IdentityServer.Core.DataAccess.Query.Entity;
using IdentityServer.Core.Validations.Common;
using IdentityServer.Domain.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Validations
{
    public class QueryBaseValidator<T> : AbstractValidator<T>
    {
        public RequestServerBoValidator RequestServerValidator { get; set; }
        
        public QueryBaseValidator()
        {
            RequestServerValidator = new RequestServerBoValidator();
        }
    }
}