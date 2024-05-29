using XFramework.Domain.Shared.Enums;

namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = TransferWalletRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record TransferWalletRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public TransferDeductionType TransferDeductionType { get; set; } = TransferDeductionType.Default;
    public TransactionPurpose TransactionPurpose { get; set; } = TransactionPurpose.Transfer;
    public required Guid RecipientCredentialId { get; set; }
    public required Guid WalletTypeId { get; set; }
}