namespace Wallets.Core.DataAccess.Commands.Handlers.Wallets;

public class CreateWalletEntityHandler : CommandBaseHandler, IRequestHandler<CreateWalletEntityCmd, CmdResponse<CreateWalletEntityCmd>>
{
    public CreateWalletEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateWalletEntityCmd>> Handle(CreateWalletEntityCmd request, CancellationToken cancellationToken)
    {
        var currencyEntity = await _dataLayer.TblCurrencyEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.CurrencyEntityGuid}", cancellationToken);
        if (currencyEntity == null)
        {
            return new()
            {
                Message = $"Currency entity with Guid {request.CurrencyEntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<TblWalletEntity>();
        entity.CurrencyEntity = currencyEntity;
        
        _dataLayer.TblWalletEntities.Add(entity);
        await _dataLayer.SaveChangesAsync(cancellationToken);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Wallet successfully created"
        };
    }
}