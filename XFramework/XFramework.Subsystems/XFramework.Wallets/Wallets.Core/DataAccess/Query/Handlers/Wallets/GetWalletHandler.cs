using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Query.Entity.Wallets;
using Wallets.Core.Interfaces;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets
{
    public class GetWalletHandler  : QueryBaseHandler, IRequestHandler<GetWalletQuery, QueryResponseBO<GetWalletContract>>
    {
        public GetWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<GetWalletContract>> Handle(GetWalletQuery request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Id.ToString(), cancellationToken: cancellationToken);
           
            if (entity == null)
            {
                return new QueryResponseBO<GetWalletContract>()
                {
                    Message = $"Identity with UID {request.Id} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return new QueryResponseBO<GetWalletContract>()
            {
                Response = entity.Adapt<GetWalletContract>()
            };
        }
    }
}