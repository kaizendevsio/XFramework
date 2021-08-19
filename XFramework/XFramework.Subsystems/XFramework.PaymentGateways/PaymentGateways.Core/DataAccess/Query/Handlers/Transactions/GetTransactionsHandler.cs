using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Core.DataAccess.Query.Entity.Transactions;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace PaymentGateways.Core.DataAccess.Query.Handlers.Transactions
{
    public class GetTransactionsHandler : QueryBaseHandler, IRequestHandler<GetTransactionsQuery, QueryResponseBO<List<UserDepositContract>>>
    {
        public GetTransactionsHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public virtual async Task<QueryResponseBO<List<UserDepositContract>>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
        {
            var credential = await _dataLayer.TblIdentityCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Cuid == request.Cuid.ToString(), cancellationToken: cancellationToken);
            
            if (credential == null)
            {
                return new()
                {
                    Message = $"Identity does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var result = await _dataLayer.TblUserDepositRequests
                .Include(i => i.Gateway)
                .Include(i => i.Currency)
                .Include(i => i.TargetWalletType)
                .Where(i => i.UserAuthId == credential.Id)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            
            if (!result.Any())
            {
                return new()
                {
                    Message = $"No transaction records exist",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }
            
            var r = result.Adapt<List<UserDepositContract>>();
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = r
            };  
        }
    }
}