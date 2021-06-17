using System.Collections.Generic;
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
    public class WalletTransactionsHandler : QueryBaseHandler, IRequestHandler<WalletTransactionsQuery, QueryResponseBO<List<WalletTransactionContract>>>
    {
        public WalletTransactionsHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<List<WalletTransactionContract>>> Handle(WalletTransactionsQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblUserWalletTransactions
                .Where(i => i.SourceUserWalletId == request.WalletId)
                .Where(i => i.UserAuth.UserName == request.UserName)
                .Include(i => i.UserAuth)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            
            if (result == null)
            {
                return new()
                {
                    Message = "The requested resource was not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = result.Adapt<List<WalletTransactionContract>>()
            };
        }
    }
}