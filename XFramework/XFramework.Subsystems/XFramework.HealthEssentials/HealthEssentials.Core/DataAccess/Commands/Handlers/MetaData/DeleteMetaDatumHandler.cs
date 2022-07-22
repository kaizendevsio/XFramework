using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class DeleteMetaDatumHandler : CommandBaseHandler, IRequestHandler<DeleteMetaDatumCmd, CmdResponse<DeleteMetaDatumCmd>>
{
    public DeleteMetaDatumHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteMetaDatumCmd>> Handle(DeleteMetaDatumCmd request, CancellationToken cancellationToken)
    {
        var existingMetaDatum = await _dataLayer.HealthEssentialsContext.MetaData.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingMetaDatum is null)
        {
            return new ()
            {
                Message = $"Meta Datum with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingMetaDatum.IsDeleted = true;
        existingMetaDatum.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingMetaDatum);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Meta Datum with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}