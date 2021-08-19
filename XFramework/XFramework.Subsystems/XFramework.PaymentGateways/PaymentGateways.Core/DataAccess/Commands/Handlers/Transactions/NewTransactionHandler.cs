using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentGateways.Core.DataAccess.Commands.Entity.Transactions;
using PaymentGateways.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace PaymentGateways.Core.DataAccess.Commands.Handlers.Transactions
{
    public class NewTransactionHandler : CommandBaseHandler, IRequestHandler<NewTransactionCmd, CmdResponseBO<NewTransactionCmd>>
    {
        public NewTransactionHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public virtual async Task<CmdResponseBO<NewTransactionCmd>> Handle(NewTransactionCmd request, CancellationToken cancellationToken)
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
            
            
            
        }
    }
}