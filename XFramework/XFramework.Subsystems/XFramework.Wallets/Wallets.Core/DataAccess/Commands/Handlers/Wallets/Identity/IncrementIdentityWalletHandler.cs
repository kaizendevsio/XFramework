using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Core.Interfaces;
using Wallets.Domain.DataTransferObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity
{
    public class IncrementIdentityWalletHandler : CommandBaseHandler,
        IRequestHandler<IncrementIdentityWalletCmd, CmdResponseBO<IncrementIdentityWalletCmd>>
    {
        public IncrementIdentityWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<IncrementIdentityWalletCmd>> Handle(IncrementIdentityWalletCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblUserWallets
                .Where(i => i.UserAuthId == request.UserCredentialId)
                .Where(i => i.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                return new CmdResponseBO<IncrementIdentityWalletCmd>()
                {
                    Message = $"Identity Id: {request.UserCredentialId} with Wallet ID: {request.WalletTypeId} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            entity.Balance += request.Amount;
            _dataLayer.Update(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = $"{request.Amount} has been added to Wallet ID:{request.WalletTypeId}"
            };
        }
        
        
        
    }
}