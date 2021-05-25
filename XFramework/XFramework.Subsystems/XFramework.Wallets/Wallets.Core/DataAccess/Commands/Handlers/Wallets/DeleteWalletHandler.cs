using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets
{
    public class DeleteWalletHandler : CommandBaseHandler, IRequestHandler<DeleteWalletCmd, CmdResponseBO<DeleteWalletCmd>>
    {
        public DeleteWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<DeleteWalletCmd>> Handle(DeleteWalletCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken: cancellationToken);
            if (entity == null)
            {
                return new CmdResponseBO<DeleteWalletCmd>()
                {
                    Message = $"Wallet with ID {request.Id} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            entity.IsDeleted = true;
            _dataLayer.Update(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Wallet successfully deleted"
            };
        }
    }
}