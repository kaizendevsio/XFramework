using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity
{
    public class GetIdentityHandler : QueryBaseHandler ,IRequestHandler<GetIdentityQuery, QueryResponseBO<GetIdentityContract>>
    {

        public GetIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<QueryResponseBO<GetIdentityContract>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Uid.ToString(), cancellationToken: cancellationToken);
           
            if (entity == null)
            {
                return new QueryResponseBO<GetIdentityContract>()
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return new QueryResponseBO<GetIdentityContract>()
            {
                Response = entity.Adapt<GetIdentityContract>()
            };
            
        }
    }
}