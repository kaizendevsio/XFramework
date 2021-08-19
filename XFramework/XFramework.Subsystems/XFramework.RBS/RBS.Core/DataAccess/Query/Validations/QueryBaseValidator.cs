using FluentValidation;
using RBS.Core.Validations.Common;

namespace RBS.Core.DataAccess.Query.Validations
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