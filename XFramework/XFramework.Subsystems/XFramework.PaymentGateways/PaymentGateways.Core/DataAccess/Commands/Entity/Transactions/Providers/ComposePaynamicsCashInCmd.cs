using MediatR;
using PaymentGateways.Domain.DataTransferObjects;

namespace PaymentGateways.Core.DataAccess.Commands.Entity.Transactions.Providers;

public class ComposePaynamicsCashInCmd : CommandBaseEntity, IRequest<QueryResponse<string>>
{
    public ProcessPaynamicsCashInCmd Request { get; set; }
    public string ClientReference { get; set; }
    public IdentityInformation IdentityInformation { get; set; }
}