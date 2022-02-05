namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets;

public class UpdateWalletEntityHandler: CommandBaseHandler, IRequestHandler<UpdateWalletEntityCmd, CmdResponseBO<UpdateWalletEntityCmd>>
{
    public UpdateWalletEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<CmdResponseBO<UpdateWalletEntityCmd>> Handle(UpdateWalletEntityCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblWalletEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}" , cancellationToken);
        if (entity == null)
        {
            return new()
            {
                Message = $"Wallet entity with data [Guid: {request.Guid}] does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        entity = request.Adapt(entity);
        _dataLayer.Update(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = $"Wallet Guid:{request.Guid} has been updated successfully."
        };
    }
}