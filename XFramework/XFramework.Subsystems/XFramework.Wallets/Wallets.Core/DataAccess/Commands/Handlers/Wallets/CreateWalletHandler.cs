using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.Interfaces;
using Wallets.Domain.DataTransferObjects;
using XFramework.Domain.Generic.BusinessObjects;


namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets
{
    public class CreateWalletHandler : CommandBaseHandler,
        IRequestHandler<CreateWalletCmd, CmdResponseBO<CreateWalletCmd>>
    {
        public CreateWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }

        public async Task<CmdResponseBO<CreateWalletCmd>> Handle(CreateWalletCmd request, CancellationToken cancellationToken)
        {
            _dataLayer.TblWalletEntities.Add(request.Adapt<TblWalletEntity>());
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Wallet successfully created"
            };
        }
    }
}