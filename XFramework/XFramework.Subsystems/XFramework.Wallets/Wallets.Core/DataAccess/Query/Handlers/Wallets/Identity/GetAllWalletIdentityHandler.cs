using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Query.Entity.Wallets;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Core.Interfaces;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets.Identity
{
    public class GetAllWalletIdentityHandler : QueryBaseHandler, IRequestHandler<GetAllWalletIdentityQuery, QueryResponseBO<List<GetWalletIdentityContract>>>
    {
        private readonly IDataLayer _dataLayer;

        public GetAllWalletIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<List<GetWalletIdentityContract>>> Handle(GetAllWalletIdentityQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblWalletEntities.Take(1000).AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
            if (!result.Any())
            {
                return new QueryResponseBO<List<GetWalletIdentityContract>>()
                {
                    Message = $"No identity exists",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }

            var r = result.Adapt<List<GetWalletIdentityContract>>();
            return new QueryResponseBO<List<GetWalletIdentityContract>>()
            {
                Response = r
            };
        }
    }
}