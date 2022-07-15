using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticRiderTagHandler : CommandBaseHandler, IRequestHandler<CreateLogisticRiderTagCmd, CmdResponse<CreateLogisticRiderTagCmd>>
{
    public CreateLogisticRiderTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticRiderTagCmd>> Handle(CreateLogisticRiderTagCmd request, CancellationToken cancellationToken)
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
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var riderTag = request.Adapt<LogisticRiderTag>();
        riderTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        riderTag.LogisticRider = rider;
        riderTag.Tag = tag;

        await _dataLayer.HealthEssentialsContext.LogisticRiderTags.AddAsync(riderTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(riderTag.Guid);
        return new()
        {
            Message = $"Logistic Rider Tag with Guid {riderTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}