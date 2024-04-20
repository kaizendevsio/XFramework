namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = ConvertWalletRequest;
using TResponse = CmdResponse;

public record ConvertWalletRequest : RequestBase, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public decimal Amount { get; set; }
    public Guid CredentialId { get; set; }
    public WalletType? FromWalletType { get; set; }
    public WalletType? ToWalletType { get; set; }
}