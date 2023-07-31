using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Core.DataAccess.Query.Entity.Gateway;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.Generic.Contracts.Responses;

namespace PaymentGateways.Core.DataAccess.Query.Handlers.Gateway;

public class GetGatewayListHandler : QueryBaseHandler, IRequestHandler<GetGatewayListQuery, QueryResponse<List<GatewayCategoryContract>>>
{
    public GetGatewayListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponse<List<GatewayCategoryContract>>> Handle(GetGatewayListQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.GatewayCategories
            .Include(i => i.Gateways)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
            
        if (!result.Any())
        {
            return new()
            {
                Message = $"No gateway records exist",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }
            
        var r = result.Adapt<List<GatewayCategoryContract>>();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = r
        };    
            
    }
}