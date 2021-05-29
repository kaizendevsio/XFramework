using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Core.Interfaces;
using Wallets.Domain.DataTransferObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity
{
    public class DeleteIdentityWalletHandler  : CommandBaseHandler,
        IRequestHandler<DeleteIdentityWalletCmd, CmdResponseBO<DeleteIdentityWalletCmd>>
    {
        public DeleteIdentityWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<DeleteIdentityWalletCmd>> Handle(DeleteIdentityWalletCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblUserWallets
                .Where(i => i.UserAuthId == request.UserAuthId)
                .Where(i => i.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                return new CmdResponseBO<DeleteIdentityWalletCmd>()
                {
                    Message = $"Identity Id: {request.UserAuthId} with Wallet ID: {request.WalletTypeId} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            entity.IsDeleted = true;
            _dataLayer.Update(entity);
            
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = $"Wallet ID:{request.WalletTypeId} has been deleted successfully."
            };
        }
    }
}