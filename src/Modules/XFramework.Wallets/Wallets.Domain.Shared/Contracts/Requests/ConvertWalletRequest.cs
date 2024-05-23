namespace Wallets.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record ConvertWalletRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<ConvertWalletRequest, TResponse>
{
    public required Guid SourceWalletTypeId { get; set; }
    public required Guid TargetWalletTypeId { get; set; }
}