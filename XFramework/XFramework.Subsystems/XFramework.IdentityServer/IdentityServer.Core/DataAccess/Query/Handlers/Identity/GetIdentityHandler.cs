using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity
{
    public class GetIdentityHandler : QueryBaseHandler ,IRequestHandler<GetIdentityQuery, QueryResponseBO<IdentityInfoContract>>
    {

        public GetIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<QueryResponseBO<IdentityInfoContract>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Uid.ToString(), cancellationToken: cancellationToken);
           
            if (entity == null)
            {
                return new QueryResponseBO<IdentityInfoContract>()
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return new QueryResponseBO<IdentityInfoContract>()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = entity.Adapt<IdentityInfoContract>()
            };
            
        }
    }
}