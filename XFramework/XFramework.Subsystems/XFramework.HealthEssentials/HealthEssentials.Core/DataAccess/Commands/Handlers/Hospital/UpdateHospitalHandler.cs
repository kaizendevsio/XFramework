using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalCmd, CmdResponse<UpdateHospitalCmd>>
{
    public UpdateHospitalHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalCmd>> Handle(UpdateHospitalCmd request, CancellationToken cancellationToken)
    {
        var existingHospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospital is null)
        {
            return new ()
            {
                Message = $"Hospital with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedHospital = request.Adapt(existingHospital);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.HospitalEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Hospital with Guid {request.EntityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospital.Entity = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedHospital);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Hospital with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}