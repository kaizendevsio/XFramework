using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets
{
    public class UpdateWalletHandler: CommandBaseHandler, IRequestHandler<UpdateWalletCmd, CmdResponseBO<UpdateWalletCmd>>
    {
        public UpdateWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<UpdateWalletCmd>> Handle(UpdateWalletCmd request, CancellationToken cancellationToken)
        {
            var entity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Id == request.Id , cancellationToken);

            if (entity == null)
            {
                return new()
                {
                    Message = $"Wallet with data [ID: {request.Id}] does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            entity = request.Adapt(entity);
            _dataLayer.Update(entity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = $"Wallet ID:{request.Id} has been updated successfully."
            };
        }
    }
}