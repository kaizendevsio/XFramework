using MediatR;

namespace PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;

public class ComposeEcLinkTransactionCmd : CommandBaseEntity, IRequest<QueryResponse<string>>
{
    public ProcessEcLinkCashInCmd Request { get; set; }
    public string ClientReference { get; set; }
}