using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticRiderTagHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticRiderTagCmd, CmdResponse<DeleteLogisticRiderTagCmd>>
{
    public DeleteLogisticRiderTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLogisticRiderTagCmd>> Handle(DeleteLogisticRiderTagCmd request, CancellationToken cancellationToken)
    {
        var existingRiderTag = await _dataLayer.HealthEssentialsContext.LogisticRiderTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingRiderTag == null)
        {
            return new()
            {
                Message = $"Rider Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingRiderTag.IsDeleted = true;
        existingRiderTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingRiderTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Rider Tag with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}