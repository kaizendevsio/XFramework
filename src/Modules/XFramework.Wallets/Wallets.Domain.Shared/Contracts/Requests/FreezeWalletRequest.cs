namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = FreezeWalletRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record FreezeWalletRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid WalletId { get; set; }
    public string? Reason { get; set; }
}
