namespace XFramework.Portal.Features.Finance.Services;

public sealed record FinancialReportDailyPoint(string Date, decimal Credits, decimal Debits)
{
    public decimal Net => Credits - Debits;
}
