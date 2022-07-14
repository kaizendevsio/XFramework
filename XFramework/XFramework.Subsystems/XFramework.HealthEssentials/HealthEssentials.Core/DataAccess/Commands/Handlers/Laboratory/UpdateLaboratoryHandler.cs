﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryCmd, CmdResponse<UpdateLaboratoryCmd>>
{
    public UpdateLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryCmd>> Handle(UpdateLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (existingLaboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var updatedLaboratory = request.Adapt(existingLaboratory);
    
        _dataLayer.HealthEssentialsContext.Update(updatedLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = "Laboratory updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = request
        };
    }
}