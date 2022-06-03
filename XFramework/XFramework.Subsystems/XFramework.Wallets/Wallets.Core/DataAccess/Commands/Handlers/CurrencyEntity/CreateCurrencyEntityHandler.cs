using Wallets.Core.DataAccess.Commands.Entity.CurrencyEntity;

namespace Wallets.Core.DataAccess.Commands.Handlers.CurrencyEntity;

public class CreateCurrencyEntityHandler : CommandBaseHandler, IRequestHandler<CreateCurrencyEntityCmd, CmdResponse<CreateCurrencyEntityCmd>>
{
    
    public CreateCurrencyEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;         
    }
    
    public async Task<CmdResponse<CreateCurrencyEntityCmd>> Handle(CreateCurrencyEntityCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<Domain.DataTransferObjects.CurrencyEntity>();
        _dataLayer.CurrencyEntities.Add(entity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "Success",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}