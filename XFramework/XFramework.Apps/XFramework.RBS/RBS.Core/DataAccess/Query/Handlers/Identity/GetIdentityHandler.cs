using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Query.Entity.Identity;
using RBS.Core.Interfaces;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts;

namespace RBS.Core.DataAccess.Query.Handlers.Identity
{
    public class GetIdentityHandler : QueryBaseHandler, IRequestHandler<GetIdentityQuery, QueryResponseBO<GetIdentityContract>>
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