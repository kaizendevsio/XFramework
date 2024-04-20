using System.Collections.Generic;
using MediatR;
using PaymentGateways.Domain.Generic.Contracts.Responses;

namespace PaymentGateways.Core.DataAccess.Query.Entity.Gateway;

public class GetGatewayListQuery : QueryBaseEntity, IRequest<QueryResponse<List<GatewayCategoryContract>>>
{
        
}