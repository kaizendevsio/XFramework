using Wallets.Core.DataAccess.Commands.Entity.ExchangeRate;

namespace Wallets.Core.DataAccess.Commands.Handlers.ExchangeRate;

public class DeleteExchangeRateHandler : CommandBaseHandler, IRequestHandler<DeleteExchangeRateCmd, CmdResponse<DeleteExchangeRateCmd>>
{
    public DeleteExchangeRateHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteExchangeRateCmd>> Handle(DeleteExchangeRateCmd request, CancellationToken cancellationToken)
    {
        var response = await _dataLayer.ExchangeRates.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (response == null)
        {
            return new()
            {
                Message = "Exchange rate not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        response.IsEnabled = false;
        _dataLayer.Update(response);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = "Success",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}