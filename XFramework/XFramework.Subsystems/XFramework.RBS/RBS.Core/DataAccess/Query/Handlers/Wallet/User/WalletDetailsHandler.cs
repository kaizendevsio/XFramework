using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Query.Entity.Wallet.User;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Handlers.Wallet.User
{
    public class WalletDetailsHandler : QueryBaseHandler, IRequestHandler<WalletDetailsQuery, QueryResponseBO<WalletDetailsContract>>
    {
        public WalletDetailsHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<WalletDetailsContract>> Handle(WalletDetailsQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblUserWallets
                .Where(i => i.WalletTypeId == request.WalletId)
                .Where(i => i.UserAuth.UserName == request.UserName)
                .Include(i => i.UserAuth)
                .Include(i => i.WalletType)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (result == null)
            {
                return new()
                {
                    Message = "The requested resource was not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            var entity = result.Adapt<WalletDetailsContract>();
            entity.WalletName = result.WalletType.Name;
            entity.WalletCode = result.WalletType.Code;
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = entity
            };
        }
    }
}