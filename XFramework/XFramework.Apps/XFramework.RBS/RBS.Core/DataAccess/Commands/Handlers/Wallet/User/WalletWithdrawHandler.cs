using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Commands.Entity.Wallet.User;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Handlers.Wallet.User
{
    public class WalletWithdrawHandler : CommandBaseHandler, IRequestHandler<WalletWithdrawCmd, CmdResponseBO<WalletWithdrawCmd>>
    {
        public WalletWithdrawHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<WalletWithdrawCmd>> Handle(WalletWithdrawCmd request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblUserWallets
                .Where(i => i.WalletTypeId == request.WalletId)
                .Where(i => i.UserAuth.UserName == request.UserName)
                .Include(i => i.UserAuth)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            
            if (result == null)
            {
                return new()
                {
                    Message = "The requested resource was not found",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Request = request
                };
            }

            result.Balance -= request.Amount;

            // Add to transaction history
            _dataLayer.TblUserWalletTransactions.Add(new()
            {
                UserAuthId = result.UserAuth.Id,
                Amount = (request.Amount * -1),
                SourceUserWalletId = result.Id,
                Remarks = "Debit" 
            });
            
            await _dataLayer.SaveChangesAsync(cancellationToken);
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Request = request
            };
        }
    }
}