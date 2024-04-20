using Coins.Core.Validations.Common;
using FluentValidation;

namespace Coins.Core.DataAccess.Query.Validations
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