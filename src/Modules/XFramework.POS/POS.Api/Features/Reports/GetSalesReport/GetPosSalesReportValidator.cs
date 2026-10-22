using FluentValidation;

namespace POS.Api.Features.Reports.GetSalesReport;

public sealed class GetPosSalesReportValidator : AbstractValidator<GetPosSalesReportRequest>
{
    public GetPosSalesReportValidator()
    {
        RuleFor(x => x.FromUtc).Must(x => x.Kind == DateTimeKind.Utc && x.TimeOfDay == TimeSpan.Zero)
            .WithMessage("From must be a UTC date.");
        RuleFor(x => x.ToUtcExclusive).Must(x => x.Kind == DateTimeKind.Utc && x.TimeOfDay == TimeSpan.Zero)
            .WithMessage("The exclusive end must be a UTC date.");
        RuleFor(x => x).Must(x => x.ToUtcExclusive > x.FromUtc &&
            x.ToUtcExclusive - x.FromUtc <= TimeSpan.FromDays(366))
            .WithMessage("Choose a date range of 1-366 days.");
        RuleFor(x => x.RegisterId).NotEqual(Guid.Empty).When(x => x.RegisterId.HasValue);
        RuleFor(x => x.CurrencyId).NotEqual(Guid.Empty).When(x => x.CurrencyId.HasValue);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.PaymentMethod).IsInEnum().When(x => x.PaymentMethod.HasValue);
        RuleFor(x => x.HourUtc).InclusiveBetween(0, 23).When(x => x.HourUtc.HasValue);
        RuleFor(x => x).Must(x => !x.HourUtc.HasValue || x.ToUtcExclusive - x.FromUtc == TimeSpan.FromDays(1))
            .WithMessage("An hour drilldown requires a one-day range.");
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.RefundPage).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
