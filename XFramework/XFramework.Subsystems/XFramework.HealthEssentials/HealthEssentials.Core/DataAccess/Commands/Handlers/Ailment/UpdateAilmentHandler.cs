using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentCmd, CmdResponse<UpdateAilmentCmd>>
{
    public UpdateAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentCmd>> Handle(UpdateAilmentCmd request, CancellationToken cancellationToken)
    {
        var existingAilment = await _dataLayer.HealthEssentialsContext.Ailments.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingAilment is null)
        {
            return new ()
            {
                Message = $"Ailment with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedAilment = request.Adapt(existingAilment);

        if (request.EntityGuid is not null)
        { 
            var ailmentEntity = await _dataLayer.HealthEssentialsContext.AilmentEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None); 
            if (ailmentEntity == null)
            {
                return new()
                {
                    Message = $"Ailment entity with Guid {request.EntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedAilment.Entity = ailmentEntity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedAilment);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
        
    }
}