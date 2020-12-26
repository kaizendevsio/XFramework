using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BO;
using IdentityServer.Domain.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity
{
    public class GetIdentityHandler : QueryBaseHandler ,IRequestHandler<GetIdentityQuery, QueryResponseBO<GetIdentityContract>>
    {

        public GetIdentityHandler(IDataLayer dataLayer, IMapper mapper)
        {
            _dataLayer = dataLayer;
            _mapper = mapper;
        }
        public async Task<QueryResponseBO<GetIdentityContract>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblIdentityInfos.FirstOrDefaultAsync(i => i.Uid == request.Uid.ToString(), cancellationToken: cancellationToken);
           
            if (result == null)
            {
                return new QueryResponseBO<GetIdentityContract>()
                {
                    Message = $"Identity with UID {request.Uid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return new QueryResponseBO<GetIdentityContract>()
            {
                Response = _mapper.Map<GetIdentityContract>(result)
            };
            
        }
    }
}