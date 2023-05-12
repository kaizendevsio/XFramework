using MediatR;
using PaymentGateways.Core.DataAccess.Commands.Entity;

namespace PaymentGateways.Core.DataAccess.Query.Entity.Transactions;

public class HandleEcLinkResponseQuery : CommandBaseEntity, IRequest<CmdResponse>
{
    public string ResponseString { get; set; }
}