using System.Collections.Generic;
using MediatR;
using PaymentGateways.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace PaymentGateways.Core.DataAccess.Query.Entity.Gateway
{
    public class GetGatewayListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GatewayCategoryContract>>>
    {
        
    }
}