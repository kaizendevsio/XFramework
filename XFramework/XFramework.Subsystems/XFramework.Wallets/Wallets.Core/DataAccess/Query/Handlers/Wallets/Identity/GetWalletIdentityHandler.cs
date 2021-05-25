using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Core.Interfaces;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets.Identity
{
    public class GetWalletIdentityHandler : QueryBaseHandler, IRequestHandler<GetIdentityWalletQuery, QueryResponseBO<GetIdentityWalletContract>>
    {
        public GetWalletIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<GetIdentityWalletContract>> Handle(GetIdentityWalletQuery request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Id.ToString(), cancellationToken: cancellationToken);
           
            if (entity == null)
            {
                return new QueryResponseBO<GetIdentityWalletContract>()
                {
                    Message = $" Wallet identity with UID {request.Id} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return new QueryResponseBO<GetIdentityWalletContract>()
            {
                Response = entity.Adapt<GetIdentityWalletContract>()
            };
        }
        
    }
}