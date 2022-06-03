using Wallets.Core.DataAccess.Commands.Entity.ExchangeRate;

namespace Wallets.Core.DataAccess.Commands.Handlers.ExchangeRate;

public class CreateExchangeRateHandler : CommandBaseHandler, IRequestHandler<CreateExchangeRateCmd, CmdResponse<CreateExchangeRateCmd>>
{
    public CreateExchangeRateHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateExchangeRateCmd>> Handle(CreateExchangeRateCmd request, CancellationToken cancellationToken)
    {
        var sourceCurrencyEntity = await _dataLayer.CurrencyEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.SourceCurrencyEntityGuid}", CancellationToken.None);
        if (sourceCurrencyEntity == null)
        {
            return new ()
            {
                Message = "Source currency entity not found",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        
        var targetCurrencyEntity = await _dataLayer.CurrencyEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.TargetCurrencyEntityGuid}", CancellationToken.None);
        if (targetCurrencyEntity == null)
        {
            return new ()
            {
                Message = "Target currency entity not found",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }

        var exchangeRates = request.Adapt<Domain.DataTransferObjects.ExchangeRate>();
        exchangeRates.SourceCurrencyEntity = sourceCurrencyEntity;
        exchangeRates.TargetCurrencyEntity = targetCurrencyEntity;

        _dataLayer.ExchangeRates.Add(exchangeRates);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = "Success",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}