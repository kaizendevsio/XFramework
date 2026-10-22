using Wallets.Domain.Shared.Contracts.Responses;

namespace Wallets.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record WalletPaymentGatewayOptionsRequest : RequestBase,
    ICommand<CmdResponse<List<WalletPaymentGatewayOptionResponse>>>,
    IBoltRequest<WalletPaymentGatewayOptionsRequest, CmdResponse<List<WalletPaymentGatewayOptionResponse>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}
