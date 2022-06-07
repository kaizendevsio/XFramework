using Wallets.Core.DataAccess.Commands.Entity.CurrencyEntity;

namespace Wallets.Core.DataAccess.Commands.Handlers.CurrencyEntity;

public class UpdateCurrencyEntityHandler : CommandBaseHandler, IRequestHandler<UpdateCurrencyEntityCmd, CmdResponse<UpdateCurrencyEntityCmd>>
{
    public UpdateCurrencyEntityHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateCurrencyEntityCmd>> Handle(UpdateCurrencyEntityCmd request, CancellationToken cancellationToken)
    {
        var currencyEntity = await _dataLayer.CurrencyEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);;
        if (currencyEntity == null)
        {
            return new()
            {
                Message = "CurrencyEntity not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        _dataLayer.CurrencyEntities.Update(currencyEntity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        return new()
        {
            Message = "CurrencyEntity updated",
            HttpStatusCode = HttpStatusCode.Accepted
        };


    }
}