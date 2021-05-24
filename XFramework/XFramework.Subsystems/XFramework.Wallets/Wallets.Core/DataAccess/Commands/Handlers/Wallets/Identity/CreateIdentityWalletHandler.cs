using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Core.Interfaces;
using Wallets.Domain.DataTransferObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity
{
    public class CreateIdentityWalletHandler  : CommandBaseHandler,
        IRequestHandler<CreateIdentityWalletCmd, CmdResponseBO<CreateIdentityWalletCmd>>
    {
        public CreateIdentityWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<CreateIdentityWalletCmd>> Handle(CreateIdentityWalletCmd request, CancellationToken cancellationToken)
        {
            _dataLayer.TblUserWallets.Add(request.Adapt<TblUserWallet>());
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Wallet identity created successfully"
            };
        }
    }
}