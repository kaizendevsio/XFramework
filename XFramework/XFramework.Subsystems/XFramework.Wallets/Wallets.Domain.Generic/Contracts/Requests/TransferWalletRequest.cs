namespace Wallets.Domain.Generic.Contracts.Requests;

using TRequest = TransferWalletRequest;
using TResponse = CmdResponse;

public record TransferWalletRequest : TransactionRequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public Guid WalletTypeId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Remarks { get; set; }
}