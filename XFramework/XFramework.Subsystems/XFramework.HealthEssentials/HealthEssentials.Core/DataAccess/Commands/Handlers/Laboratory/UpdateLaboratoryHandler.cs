using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryCmd, CmdResponse<UpdateLaboratoryCmd>>
{
    public UpdateLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryCmd>> Handle(UpdateLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (existingLaboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedLaboratory = request.Adapt(existingLaboratory);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.LaboratoryEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Laboratory entity with Guid {request.EntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLaboratory.Entity = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}