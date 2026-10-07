using FluentValidation;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Reports.Financial;

public sealed class WalletFinancialReportValidator : AbstractValidator<WalletFinancialReportRequest>
{
    public WalletFinancialReportValidator()
    {
        RuleFor(x => x.From).NotEmpty().Must(x => x.Kind == DateTimeKind.Utc).WithMessage("From must be a UTC date.");
        RuleFor(x => x.ToExclusive).NotEmpty().Must(x => x.Kind == DateTimeKind.Utc).WithMessage("Through date must be a UTC date.");
        RuleFor(x => x).Must(x => x.ToExclusive > x.From && x.ToExclusive - x.From <= TimeSpan.FromDays(366))
            .WithMessage("Choose a reporting period of up to 366 days.");
        RuleFor(x => x.WalletId).NotEqual(Guid.Empty).When(x => x.WalletId.HasValue);
    }
}
