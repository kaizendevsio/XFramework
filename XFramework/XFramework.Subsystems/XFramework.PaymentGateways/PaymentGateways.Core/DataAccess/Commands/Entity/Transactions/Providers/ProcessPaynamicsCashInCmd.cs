using MediatR;
using PaymentGateways.Domain.Contracts.Responses;
using PaymentGateways.Domain.DataTransferObjects;
using Wallets.Domain.Generic.Contracts.Responses;

namespace PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;

public class ProcessPaynamicsCashInCmd : CommandBaseEntity, IRequest<CmdResponse<ProcessTransactionResponse>>
{
    public IdentityCredential Credential { get; set; }
    public WalletResponse Wallet { get; set; }
    public Gateway Gateway { get; set; }
    public string ReferenceNumber { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
}