namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = CloseWalletRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CloseWalletRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid WalletId { get; set; }
    public string? Reason { get; set; }
}
