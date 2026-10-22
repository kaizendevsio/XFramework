using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

namespace Inventario.Api.Features.Transactions.ProductSales;

public sealed class GetInventoryProductSalesValidator : AbstractValidator<GetInventoryProductSalesRequest>
{
    public GetInventoryProductSalesValidator()
    {
        RuleFor(x => x.FromUtc).Must(x => x.Kind == DateTimeKind.Utc && x.TimeOfDay == TimeSpan.Zero).WithMessage("From must be a UTC date.");
        RuleFor(x => x.ToUtcExclusive).Must(x => x.Kind == DateTimeKind.Utc && x.TimeOfDay == TimeSpan.Zero).WithMessage("The exclusive end must be a UTC date.");
        RuleFor(x => x).Must(x => x.ToUtcExclusive > x.FromUtc && x.ToUtcExclusive - x.FromUtc <= TimeSpan.FromDays(366))
            .WithMessage("Choose a date range of 1-366 days.");
        RuleFor(x => x.Skip).InclusiveBetween(0, 10000000);
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
        RuleFor(x => x.ProductFilter).MaximumLength(200);
        RuleFor(x => x.ProductFilterOperator).Must(x => x is null or "Contains" or "NotContains" or "Equals" or "NotEquals" or "StartsWith" or "EndsWith" or "IsEmpty" or "IsNotEmpty");
    }
}
