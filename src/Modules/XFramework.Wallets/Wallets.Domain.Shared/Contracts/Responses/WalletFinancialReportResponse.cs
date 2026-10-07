namespace Wallets.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record WalletFinancialReportResponse
{
    public Guid TenantId { get; init; }
    public DateTime GeneratedAt { get; init; }
    public List<WalletFinancialCurrencySummary> Currencies { get; init; } = [];
    public List<WalletFinancialDailyActivity> DailyActivity { get; init; } = [];
}

[MemoryPackable]
public partial record WalletFinancialCurrencySummary
{
    public Guid? CurrencyId { get; init; }
    public string Currency { get; init; } = "Unspecified";
    public int WalletCount { get; init; }
    public decimal Balance { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal DebitHold { get; init; }
    public decimal CreditHold { get; init; }
    public decimal Credits { get; init; }
    public decimal Debits { get; init; }
    public decimal Fees { get; init; }
    public int EntryCount { get; init; }
}

[MemoryPackable]
public partial record WalletFinancialDailyActivity
{
    public Guid? CurrencyId { get; init; }
    public DateTime Date { get; init; }
    public decimal Credits { get; init; }
    public decimal Debits { get; init; }
}
