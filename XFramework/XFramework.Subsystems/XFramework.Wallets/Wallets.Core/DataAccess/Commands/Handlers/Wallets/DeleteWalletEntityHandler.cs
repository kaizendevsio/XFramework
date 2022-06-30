namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets;

public class DeleteWalletEntityHandler : CommandBaseHandler, IRequestHandler<DeleteWalletEntityCmd, CmdResponse<DeleteWalletEntityCmd>>
{
    public DeleteWalletEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponse<DeleteWalletEntityCmd>> Handle(DeleteWalletEntityCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.WalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (entity == null)
        {
            return new CmdResponse<DeleteWalletEntityCmd>()
            {
                Message = $"Wallet entity with Guid {request.Guid} does not exist",
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
            Message = "Wallet successfully deleted"
        };
    }
}