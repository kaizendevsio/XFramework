using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts;
using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests;

public record ConvertWalletRequest : RequestBase, IRequest<CmdResponse>
{
    public decimal Amount { get; set; }
    public Guid CredentialId { get; set; }
    public WalletType? FromWalletType { get; set; }
    public WalletType? ToWalletType { get; set; }
}