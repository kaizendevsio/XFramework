using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests;

public record TransferWalletRequest : TransactionRequestBase, IRequest<CmdResponse>
{
    public Guid CredentialId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public Guid WalletTypeId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Remarks { get; set; }
}