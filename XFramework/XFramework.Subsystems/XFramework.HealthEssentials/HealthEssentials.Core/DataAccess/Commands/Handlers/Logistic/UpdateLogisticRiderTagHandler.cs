using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticRiderTagHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticRiderTagCmd, CmdResponse<UpdateLogisticRiderTagCmd>>
{
    public UpdateLogisticRiderTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLogisticRiderTagCmd>> Handle(UpdateLogisticRiderTagCmd request, CancellationToken cancellationToken)
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
        var updatedRiderTag = request.Adapt(existingRiderTag);

        if (request.LogisticRiderGuid is not null)
        {
            var rider = await _dataLayer.HealthEssentialsContext.LogisticRiders.FirstOrDefaultAsync(x => x.Guid == $"{request.LogisticRiderGuid}", CancellationToken.None);
            if (rider is null)
            {
                return new ()
                {
                    Message = $"Logistic Rider with Guid {request.LogisticRiderGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedRiderTag.LogisticRider = rider;
        }

        if (request.TagGuid is not null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag is null)
            {
                return new ()
                {
                    Message = $"Tag with Guid {request.TagGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedRiderTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedRiderTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Rider Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}