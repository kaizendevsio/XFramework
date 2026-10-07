using Wallets.Domain.Shared.Contracts.Responses;

namespace Wallets.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record WalletFinancialReportRequest : RequestBase,
    ICommand<CmdResponse<WalletFinancialReportResponse>>,
    IBoltRequest<WalletFinancialReportRequest, CmdResponse<WalletFinancialReportResponse>>
{
    public Guid? WalletId { get; init; }
    public DateTime From { get; init; }
    public DateTime ToExclusive { get; init; }
}
