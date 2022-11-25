using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalServiceHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalServiceCmd, CmdResponse<UpdateHospitalServiceCmd>>
{
    public UpdateHospitalServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalServiceCmd>> Handle(UpdateHospitalServiceCmd request, CancellationToken cancellationToken)
    {
        var existingService = await _dataLayer.HealthEssentialsContext.HospitalServices.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingService is null)
        {
            return new ()
            {
                Message = $"Hospital Service with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedService = request.Adapt(existingService);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.HospitalServiceEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new ()
                {
                    Message = $"Hospital Service with Guid {request.EntityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedService.Entity = entity;
        }

        if (request.HospitalGuid is null)
        {
            var hospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalGuid}", CancellationToken.None);
            if (hospital is null)
            {
                return new ()
                {
                    Message = $"Hospital with Guid {request.HospitalGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedService.Hospital = hospital;
        }

        if (request.HospitalLocationGuid is null)
        {
            var hospitalLocation = await _dataLayer.HealthEssentialsContext.HospitalLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalLocationGuid}", CancellationToken.None);
            if (hospitalLocation is null)
            {
                return new ()
                {
                    Message = $"Hospital Location with Guid {request.HospitalLocationGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedService.HospitalLocation = hospitalLocation;
        }

        if (request.UnitGuid is null)
        {
            var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.UnitGuid}", CancellationToken.None);
            if (unit is null)
            {
                return new ()
                {
                    Message = $"Unit with Guid {request.UnitGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedService.Unit = unit;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedService);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital Service with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}