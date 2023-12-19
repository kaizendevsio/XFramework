using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Domain.Generic.Contracts;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Entity.Models.Requests.Wallet;

public class SendWalletRequest : NavigableRequest
{
    public Guid SenderCredentialId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public Guid WalletTypeId { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalAmount => Amount + Fee;
    public decimal Fee { get; set; }
    public string? Remarks { get; set; }
    public string? ClientReference { get; set; }
}