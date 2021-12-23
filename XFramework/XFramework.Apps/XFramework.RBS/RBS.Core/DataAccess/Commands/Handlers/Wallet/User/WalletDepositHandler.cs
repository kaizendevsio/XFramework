using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Commands.Entity.Identity;
using RBS.Core.DataAccess.Commands.Entity.Wallet.User;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Handlers.Wallet.User
{
    public class WalletDepositHandler : CommandBaseHandler, IRequestHandler<WalletDepositCmd, CmdResponseBO<WalletDepositCmd>>
    {
        public WalletDepositHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
            _mediator = mediator;
        }
        public async Task<CmdResponseBO<WalletDepositCmd>> Handle(WalletDepositCmd request, CancellationToken cancellationToken)
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

            result.Balance += request.Amount;

            // Add to transaction history
            _dataLayer.TblUserWalletTransactions.Add(new()
            {
                UserAuthId = result.UserAuth.Id,
                Amount = request.Amount,
                SourceUserWalletId = result.Id,
                Remarks = "Credit"
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