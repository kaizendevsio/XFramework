using Wallets.Core.DataAccess.Commands.Entity.CurrencyEntity;
using Wallets.Core.DataAccess.Commands.Entity.ExchangeRate;

namespace Wallets.Core.DataAccess.Commands.Handlers.ExchangeRate;

public class UpdateExchangeRateHandler : CommandBaseHandler, IRequestHandler<UpdateExchangeRateCmd, CmdResponse<UpdateExchangeRateCmd>>
{
    public UpdateExchangeRateHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateExchangeRateCmd>> Handle(UpdateExchangeRateCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.ExchangeRates.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingRecord == null)
        {
            return new()
            {
                Message = "Exchange rate not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

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

        var exchangeRates = request.Adapt(existingRecord);
        exchangeRates.SourceCurrencyEntity = sourceCurrencyEntity;
        exchangeRates.TargetCurrencyEntity = targetCurrencyEntity;
        
        _dataLayer.ExchangeRates.Update(exchangeRates);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = "Exchange rate updated",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}