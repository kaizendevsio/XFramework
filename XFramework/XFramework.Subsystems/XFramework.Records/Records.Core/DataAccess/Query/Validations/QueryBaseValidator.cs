using FluentValidation;
using IdentityServer.Domain.BusinessObjects;
using Records.Core.DataAccess.Query.Entity;
using Records.Core.Validations.Common;

namespace Records.Core.DataAccess.Query.Validations
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