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
    public class GetWalletIdentityHandler : QueryBaseHandler, IRequestHandler<GetWalletIdentityQuery, QueryResponseBO<GetWalletIdentityContract>>
    {
        public GetWalletIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<GetWalletIdentityContract>> Handle(GetWalletIdentityQuery request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Uuid == request.Id.ToString(), cancellationToken: cancellationToken);
           
            if (entity == null)
            {
                return new QueryResponseBO<GetWalletIdentityContract>()
                {
                    Message = $" Wallet identity with UID {request.Id} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return new QueryResponseBO<GetWalletIdentityContract>()
            {
                Response = entity.Adapt<GetWalletIdentityContract>()
            };
        }
        
    }
}