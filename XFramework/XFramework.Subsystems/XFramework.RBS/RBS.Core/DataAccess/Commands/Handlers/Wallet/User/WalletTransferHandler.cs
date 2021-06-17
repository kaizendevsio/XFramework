using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RBS.Core.DataAccess.Commands.Entity.Wallet.User;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace RBS.Core.DataAccess.Commands.Handlers.Wallet.User
{
    public class WalletTransferHandler : CommandBaseHandler, IRequestHandler<WalletTransferCmd, CmdResponseBO<WalletTransferCmd>>
    {
        public WalletTransferHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
            _mediator = mediator;
        }
        public async Task<CmdResponseBO<WalletTransferCmd>> Handle(WalletTransferCmd request, CancellationToken cancellationToken)
        {
            var resultSource = await _dataLayer.TblUserWallets
                .Where(i => i.WalletTypeId == request.SourceWalletId)
                .Where(i => i.UserAuth.UserName == request.UserName)
                .Include(i => i.UserAuth)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            
            if (resultSource == null)
            {
                return new()
                {
                    Message = "The source wallet was not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var resultTarget = await _dataLayer.TblUserWallets
                .Where(i => i.WalletTypeId == request.TargetWalletId)
                .Where(i => i.UserAuth.UserName == request.UserName)
                .Include(i => i.UserAuth)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            
            if (resultTarget == null)
            {
                return new()
                {
                    Message = "The target wallet was not found",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Request = request
                };
            }

            if (resultSource.Balance < request.Amount)
            {
                return new()
                {
                    Message = "The requested amount is greater than current balance",
                    HttpStatusCode = HttpStatusCode.NotAcceptable,
                    Request = request
                };
            }
            
            if (request.Amount < 1)
            {
                return new()
                {
                    Message = "The requested amount should be greater than 1",
                    HttpStatusCode = HttpStatusCode.NotAcceptable,
                    Request = request
                };
            }

            // Transfer balance 
            resultTarget.Balance += request.Amount;
            resultSource.Balance -= request.Amount;
            
            // Add to transaction history
            _dataLayer.TblUserWalletTransactions.Add(new()
            {
                UserAuthId = resultTarget.UserAuthId,
                Amount = request.Amount,
                SourceUserWalletId = resultTarget.Id,
                Remarks = "Credit Transfer"
            });
            
            _dataLayer.TblUserWalletTransactions.Add(new()
            {
                UserAuthId = resultSource.UserAuthId,
                Amount = (request.Amount * -1),
                SourceUserWalletId = resultSource.Id,
                Remarks = "Debit Transfer"
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