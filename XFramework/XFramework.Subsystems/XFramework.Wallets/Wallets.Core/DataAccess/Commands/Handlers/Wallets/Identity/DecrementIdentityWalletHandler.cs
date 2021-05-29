using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity
{
    public class DecrementIdentityWalletHandler : CommandBaseHandler,
        IRequestHandler<DecrementIdentityWalletCmd, CmdResponseBO<DecrementIdentityWalletCmd>>
    {
        public DecrementIdentityWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<DecrementIdentityWalletCmd>> Handle(DecrementIdentityWalletCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblUserWallets
                .Where(i => i.UserAuthId == request.UserAuthId)
                .Where(i => i.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                return new CmdResponseBO<DecrementIdentityWalletCmd>()
                {
                    Message = $"Identity Id: {request.UserAuthId} with Wallet ID: {request.WalletTypeId} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            entity.Balance -= request.Amount;
            _dataLayer.Update(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = $"{request.Amount} has been deducted to Wallet ID:{request.WalletTypeId}"
            };
        }
    }
}