namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = UnfreezeWalletRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record UnfreezeWalletRequest : RequestBase,
    ICommand<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid WalletId { get; set; }
    public string? Reason { get; set; }
}
