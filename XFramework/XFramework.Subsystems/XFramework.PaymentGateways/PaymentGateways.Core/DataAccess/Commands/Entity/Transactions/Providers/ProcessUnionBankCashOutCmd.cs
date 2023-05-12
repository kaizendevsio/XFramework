using MediatR;
using PaymentGateways.Domain.Contracts.Responses;
using PaymentGateways.Domain.DataTransferObjects;

namespace PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;

public class ProcessUnionBankCashOutCmd : CommandBaseEntity, IRequest<CmdResponse<ProcessTransactionResponse>>
{
    public IdentityCredential Credential { get; set; }
    public Gateway Gateway { get; set; }
    public string ReferenceNo { get; set; }
}