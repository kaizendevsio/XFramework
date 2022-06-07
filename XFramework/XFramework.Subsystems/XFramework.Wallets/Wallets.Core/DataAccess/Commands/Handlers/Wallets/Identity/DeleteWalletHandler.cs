using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;

namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets.Identity;

public class DeleteWalletHandler  : CommandBaseHandler, IRequestHandler<DeleteWalletCmd, CmdResponse<DeleteWalletCmd>>
{
    public DeleteWalletHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse<DeleteWalletCmd>> Handle(DeleteWalletCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.Wallets.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (entity == null)
        {
            return new ()
            {
                Message = $"Wallet with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        entity.IsDeleted = true;
        entity.IsEnabled = false;
        _dataLayer.Update(entity);
            
        await _dataLayer.SaveChangesAsync(cancellationToken);
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = $"Wallet Guid:{request.Guid} has been deleted successfully."
        };
    }
}