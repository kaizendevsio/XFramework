using Wallets.Core.DataAccess.Commands.Entity.CurrencyEntity;

namespace Wallets.Core.DataAccess.Commands.Handlers.CurrencyEntity;

public class DeleteCurrencyEntityHandler : CommandBaseHandler, IRequestHandler<DeleteCurrencyEntityCmd, CmdResponse<DeleteCurrencyEntityCmd>>
{
    public DeleteCurrencyEntityHandler()
    {
        
    }
    
    public async Task<CmdResponse<DeleteCurrencyEntityCmd>> Handle(DeleteCurrencyEntityCmd request, CancellationToken cancellationToken)
    {
        var currencyEntity = await _dataLayer.CurrencyEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (currencyEntity == null)
        {
            return new()
            {
                Message = "CurrencyEntity not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        currencyEntity.IsEnabled = false;
        _dataLayer.Update(currencyEntity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "CurrencyEntity deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}