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
    public class TransferIdentityWalletHandler : CommandBaseHandler,
        IRequestHandler<TransferIdentityWalletCmd, CmdResponseBO<TransferIdentityWalletCmd>>
    {
        public TransferIdentityWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        
        public async Task<CmdResponseBO<TransferIdentityWalletCmd>> Handle(TransferIdentityWalletCmd request, CancellationToken cancellationToken)
        {
            var fromEntity = await _dataLayer.TblUserWallets
                .Where(i => i.UserAuthId == request.FromUserCredentialId)
                .Where(i => i.WalletTypeId == request.FromWalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);
            
            var toEntity = await _dataLayer.TblUserWallets
                .Where(i => i.UserAuthId == request.ToUserCredentialId)
                .Where(i => i.WalletTypeId == request.ToWalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);


            if (fromEntity == null || toEntity == null)
            {
                return new CmdResponseBO<TransferIdentityWalletCmd>()
                {
                    Message = $"Identity Id: {request.ToUserCredentialId} with Wallet ID: {request.ToUserCredentialId} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }


            _dataLayer.TblUserWalletTransactions.Add(new ()
            {
                UserAuthId = (long) request.FromUserCredentialId,
                Amount = request.Amount,
                SourceUserWalletId = fromEntity.Id,
                TargetUserWalletId = toEntity.Id,
                RunningBalance = fromEntity.Balance,
                Remarks = "Success"

            });
            
            fromEntity.Balance -= request.Amount;
            toEntity.Balance += request.Amount;
            
            _dataLayer.Update(fromEntity);
            _dataLayer.Update(toEntity);
            await _dataLayer.SaveChangesAsync(cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = $"You transferred {request.Amount} to Wallet ID:{request.ToWalletTypeId}"
            };


        }

    }
}